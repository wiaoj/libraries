using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Querying.AspNetCore.Binders;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "EndpointParameterOverrides")]
public sealed class EndpointParameterOverrideTests {
    private sealed class Product {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Limit { get; set; }
    }

    private static QuerySchema<Product> CreateProductSchema() {
        return new QuerySchema<Product>()
            .AllowFilter(x => x.Name)
            .AllowFilter(x => x.Price)
            .AllowFilter(x => x.Limit);
    }

    [Fact]
    public async Task Should_Ignore_Parameter_At_Endpoint_Level_Without_Schema_Modification() {
        // Arrange: Endpoint ignores "format" and "delimiter"
        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.IgnoreParameters("format", "delimiter");

        DefaultHttpContext httpContext = new();
        httpContext.Request.QueryString = new QueryString("?name=Laptop&format=csv&delimiter=pipe");

        // Act: Bind with endpoint options
        QueryRequest request = await QueryRequestBinder.BindAsync(httpContext, schema: null, endpointOptions);

        // Assert: only 'name' filter is bound; 'format' and 'delimiter' are omitted
        Assert.Single(request.Filters);
        Assert.Equal("name", request.Filters[0].Field);
        Assert.Equal("Laptop", request.Filters[0].RawValue);
    }

    [Fact]
    public async Task Should_Unignore_Parameter_At_Endpoint_Level_When_Globally_Ignored() {
        // Arrange: Global options ignore "limit", but endpoint allows "limit"
        ServiceCollection services = new();
        services.Configure<QueryOptions>(opt => opt.IgnoredParameters.Add("limit"));
        ServiceProvider sp = services.BuildServiceProvider();

        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.AllowParameters("limit");

        DefaultHttpContext httpContext = new() {
            RequestServices = sp
        };
        httpContext.Request.QueryString = new QueryString("?limit=500");

        // Act: Bind with endpoint options
        QueryRequest request = await QueryRequestBinder.BindAsync(httpContext, schema: null, endpointOptions);

        // Assert: 'limit' is un-ignored and successfully bound
        Assert.Single(request.Filters);
        Assert.Equal("limit", request.Filters[0].Field);
        Assert.Equal("500", request.Filters[0].RawValue);
    }

    [Fact]
    public async Task Should_Bypass_Global_Ignored_Parameters_At_Endpoint_Level() {
        // Arrange: Global options ignore "page", but endpoint bypasses all global ignored parameters
        ServiceCollection services = new();
        services.Configure<QueryOptions>(opt => {
            opt.IgnoredParameters.Add("page");
            opt.IgnoredParameters.Add("size");
        });
        ServiceProvider sp = services.BuildServiceProvider();

        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.IgnoreGlobalParameters();

        DefaultHttpContext httpContext = new() {
            RequestServices = sp
        };
        httpContext.Request.QueryString = new QueryString("?page=2");

        // Act
        QueryRequest request = await QueryRequestBinder.BindAsync(httpContext, schema: null, endpointOptions);

        // Assert: 'page' is NOT ignored, it is bound as a filter
        Assert.Single(request.Filters);
        Assert.Equal("page", request.Filters[0].Field);
        Assert.Equal("2", request.Filters[0].RawValue);

        // And when validated against Product schema (which does not allow 'page'), it produces a validation problem
        QuerySchema<Product> schema = CreateProductSchema();
        QueryValidationEndpointFilter<Product> filter = new(schema, endpointOptions);
        DefaultEndpointFilterInvocationContext context = new(httpContext, new Query<Product>(request));

        object? result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.IsType<ProblemHttpResult>(result);
    }

    [Fact]
    public async Task Should_Apply_Precedence_EndpointAllowed_Over_EndpointIgnored() {
        // Arrange: Both Ignore and Allow are configured for "format" on the same endpoint (Allow wins)
        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.IgnoreParameters("format");
        endpointOptions.AllowParameters("format");

        DefaultHttpContext httpContext = new();
        httpContext.Request.QueryString = new QueryString("?format=pdf");

        // Act
        QueryRequest request = await QueryRequestBinder.BindAsync(httpContext, schema: null, endpointOptions);

        // Assert: 'format' is bound because AllowParameters takes precedence
        Assert.Single(request.Filters);
        Assert.Equal("format", request.Filters[0].Field);
    }

    [Fact]
    public async Task Should_Apply_Precedence_EndpointIgnored_Over_SchemaAllowed() {
        // Arrange: Schema allows "limit", but this specific endpoint ignores "limit"
        QuerySchema<Product> schema = CreateProductSchema();
        schema.AllowParameters("limit");

        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.IgnoreParameters("limit");

        DefaultHttpContext httpContext = new();
        httpContext.Request.QueryString = new QueryString("?limit=100");

        // Act
        QueryRequest request = await QueryRequestBinder.BindAsync(httpContext, schema, endpointOptions);

        // Assert: 'limit' is ignored because endpoint-level ignore takes precedence over schema-level allow
        Assert.Empty(request.Filters);
    }

    [Fact]
    public async Task Should_Handle_Bracketed_Syntax_In_Endpoint_Overrides() {
        // Arrange: Endpoint ignores "format", request has bracket syntax
        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.IgnoreParameters("format");
        endpointOptions.AllowParameters("limit");

        ServiceCollection services = new();
        services.Configure<QueryOptions>(opt => opt.IgnoredParameters.Add("limit"));
        ServiceProvider sp = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() {
            RequestServices = sp
        };
        httpContext.Request.QueryString = new QueryString("?format[eq]=csv&limit[gte]=50");

        // Act
        QueryRequest request = await QueryRequestBinder.BindAsync(httpContext, schema: null, endpointOptions);

        // Assert: format is stripped and ignored; limit is stripped and allowed
        Assert.Single(request.Filters);
        Assert.Equal("limit", request.Filters[0].Field);
        Assert.Equal(QueryOperator.GreaterThanOrEqual, request.Filters[0].Operator);
        Assert.Equal("50", request.Filters[0].RawValue);
    }

    [Fact]
    public async Task Should_Filter_Out_Endpoint_Ignored_Parameters_In_QueryValidationEndpointFilter() {
        // Arrange: schema does not have "exportType", but endpoint options ignore "exportType"
        QuerySchema<Product> schema = CreateProductSchema();
        QueryValidationEndpointOptions endpointOptions = new();
        endpointOptions.IgnoreParameters("exportType");

        QueryRequest requestWithManualFilter = new(filters: [
            new FilterConditionNode("name", QueryOperator.Equal, "Phone"),
            new FilterConditionNode("exportType", QueryOperator.Equal, "summary")
        ]);

        DefaultHttpContext httpContext = new();
        QueryValidationEndpointFilter<Product> filter = new(schema, endpointOptions);
        DefaultEndpointFilterInvocationContext context = new(httpContext, new Query<Product>(requestWithManualFilter));

        bool nextCalled = false;

        // Act
        object? result = await filter.InvokeAsync(context, _ => {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok("Success"));
        });

        // Assert: exportType error was filtered out, validation succeeded
        Assert.True(nextCalled);
        Assert.IsType<Ok<string>>(result);
    }

    [Fact]
    public void Should_Support_Extension_Methods_IgnoreQueryParameters_And_AllowQueryParameters() {
        // Arrange
        TestEndpointConventionBuilder builder = new();

        // Act
        builder
            .IgnoreQueryParameters("format", "export")
            .AllowQueryParameters("limit")
            .IgnoreGlobalQueryParameters();

        // Assert
        TestEndpointBuilder endpointBuilder = new();
        foreach(Action<EndpointBuilder> convention in builder.Conventions) {
            convention(endpointBuilder);
        }

        QueryValidationEndpointOptions? options = endpointBuilder.Metadata.OfType<QueryValidationEndpointOptions>().LastOrDefault();
        Assert.NotNull(options);
        Assert.Contains("format", options.IgnoredParameters);
        Assert.Contains("export", options.IgnoredParameters);
        Assert.Contains("limit", options.AllowedParameters);
        Assert.True(options.IgnoresGlobalParameters);
    }

    [Fact]
    public void Should_Support_IgnoreGlobalQueryParameters_Overloads() {
        // Arrange
        TestEndpointConventionBuilder builder1 = new();
        TestEndpointConventionBuilder builder2 = new();

        // Act
        builder1.IgnoreGlobalQueryParameters();
        builder2.IgnoreGlobalQueryParameters(false);

        TestEndpointBuilder ep1 = new();
        TestEndpointBuilder ep2 = new();

        foreach(Action<EndpointBuilder> convention in builder1.Conventions) convention(ep1);
        foreach(Action<EndpointBuilder> convention in builder2.Conventions) convention(ep2);

        QueryValidationEndpointOptions? opts1 = ep1.Metadata.OfType<QueryValidationEndpointOptions>().LastOrDefault();
        QueryValidationEndpointOptions? opts2 = ep2.Metadata.OfType<QueryValidationEndpointOptions>().LastOrDefault();

        // Assert
        Assert.NotNull(opts1);
        Assert.True(opts1.IgnoresGlobalParameters);
        Assert.NotNull(opts2);
        Assert.False(opts2.IgnoresGlobalParameters);
    }

    private sealed class TestEndpointConventionBuilder : IEndpointConventionBuilder {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention) {
            this.Conventions.Add(convention);
        }
    }

    private sealed class TestEndpointBuilder : EndpointBuilder {
        public override Endpoint Build() => new(RequestDelegate, new EndpointMetadataCollection(Metadata), DisplayName);
    }

    private sealed class DefaultEndpointFilterInvocationContext(HttpContext httpContext, params object?[] arguments) : EndpointFilterInvocationContext {
        private readonly IList<object?> _arguments = arguments.ToList();

        public override HttpContext HttpContext => httpContext;
        public override IList<object?> Arguments => this._arguments;
        public override T GetArgument<T>(int index) => (T)this._arguments[index]!;
    }
}
