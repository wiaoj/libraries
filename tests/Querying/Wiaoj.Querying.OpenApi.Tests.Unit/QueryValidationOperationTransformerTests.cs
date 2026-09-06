using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.OpenApi;

namespace Wiaoj.Querying.OpenApi.Tests.Unit;

/// <summary>
/// QueryRequest binds through BindAsync, which document generation treats as opaque — so without this
/// transformer an endpoint accepting a query documents no parameters at all. These pin that what gets
/// published is what the schema actually enforces.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "OpenApi")]
public sealed class QueryValidationOperationTransformerTests {

    private sealed class Product {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private static OpenApiOperation Transform(
        QuerySchema<Product>? schema,
        bool markEndpoint = true,
        QueryValidationEndpointOptions? endpointOptions = null) {

        ServiceCollection services = new();
        if(schema is not null) {
            services.AddSingleton(schema);
        }

        List<object> metadata = [];
        if(markEndpoint) {
            metadata.Add(new QueryValidationEndpointMetadata(typeof(Product)));
        }
        if(endpointOptions is not null) {
            metadata.Add(endpointOptions);
        }

        ApiDescription description = new() {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = metadata }
        };

        OpenApiOperation operation = new() {
            Parameters = [],
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "OK" } }
        };

        OpenApiOperationTransformerContext context = new() {
            DocumentName = "v1",
            Description = description,
            ApplicationServices = services.BuildServiceProvider()
        };

        new QueryValidationOperationTransformer()
            .TransformAsync(operation, context, TestContext.Current.CancellationToken)
            .GetAwaiter().GetResult();

        return operation;
    }

    private static OpenApiParameter? Parameter(OpenApiOperation operation, string name) {
        return operation.Parameters?
            .OfType<OpenApiParameter>()
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_Publish_One_Parameter_Per_Filterable_Field() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();
        schema.Property(p => p.Price).AllowFilter();

        OpenApiOperation operation = Transform(schema);

        Assert.NotNull(Parameter(operation, nameof(Product.Name)));
        Assert.NotNull(Parameter(operation, nameof(Product.Price)));
    }

    [Fact]
    public void Should_Spell_Out_Only_The_Operators_A_Field_Permits() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Price).AllowFilter(QueryOperator.GreaterThanOrEqual, QueryOperator.LessThanOrEqual);

        OpenApiParameter price = Assert.IsType<OpenApiParameter>(Parameter(Transform(schema), nameof(Product.Price)));

        Assert.Contains("gte", price.Description, StringComparison.Ordinal);
        Assert.Contains("lte", price.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("contains", price.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_Not_Publish_A_Field_That_Cannot_Be_Filtered() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowSort();

        OpenApiOperation operation = Transform(schema);

        Assert.Null(Parameter(operation, nameof(Product.Name)));
    }

    [Fact]
    public void Should_List_The_Sortable_Fields_On_The_Sort_Parameter() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowSort();
        schema.Property(p => p.Price).AllowFilter();

        OpenApiParameter sort = Assert.IsType<OpenApiParameter>(Parameter(Transform(schema), QuerySyntax.Parameters.Sort));

        Assert.Contains(nameof(Product.Name), sort.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(Product.Price), sort.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Should_Omit_The_Sort_Parameter_When_Nothing_Is_Sortable() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();

        Assert.Null(Parameter(Transform(schema), QuerySyntax.Parameters.Sort));
    }

    [Fact]
    public void Should_Document_The_400_A_Violating_Query_Receives() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();

        Assert.True(Transform(schema).Responses!.ContainsKey("400"));
    }

    [Fact]
    public void Should_Skip_A_Parameter_The_Endpoint_Ignores() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();
        schema.Property(p => p.Price).AllowFilter();

        QueryValidationEndpointOptions options = new();
        options.IgnoreParameters(nameof(Product.Price));

        OpenApiOperation operation = Transform(schema, endpointOptions: options);

        Assert.NotNull(Parameter(operation, nameof(Product.Name)));
        Assert.Null(Parameter(operation, nameof(Product.Price)));
    }

    [Fact]
    public void Should_Leave_An_Unmarked_Endpoint_Untouched() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();

        OpenApiOperation operation = Transform(schema, markEndpoint: false);

        Assert.Empty(operation.Parameters!);
        Assert.False(operation.Responses!.ContainsKey("400"));
    }

    [Fact]
    public void Should_Invent_Nothing_When_No_Schema_Is_Registered() {
        OpenApiOperation operation = Transform(schema: null);

        Assert.Empty(operation.Parameters!);
        Assert.False(operation.Responses!.ContainsKey("400"));
    }
}
