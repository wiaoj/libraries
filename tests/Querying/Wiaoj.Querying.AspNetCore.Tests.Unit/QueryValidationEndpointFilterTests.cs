using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

/// <summary>
/// Comprehensive unit test suite for <see cref="QueryValidationEndpointFilter{T}"/> and
/// <see cref="EndpointRouteBuilderExtensions"/> validating schema rule enforcement,
/// RFC 7807 problem details payloads, argument resolution across parameter positions, and security limits.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "AspNetCoreValidationFilter")]
public class QueryValidationEndpointFilterTests {
    private enum Status { Active, Inactive, Archived }

    private sealed class Item {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? InternalTag { get; set; }
    }

    private static QuerySchema<Item> CreateSampleSchema() {
        return new QuerySchema<Item>()
            .AllowFilter(x => x.Title)
            .Property(x => x.Price, p => p.AllowFilter(QueryOperator.Equal, QueryOperator.GreaterThanOrEqual).AllowSort())
            .AllowFilter(x => x.Status, x => x.CreatedAt)
            .AllowSort(x => x.CreatedAt)
            .ConfigureLimits(maxFilters: 3, maxInValues: 4, maxSortFields: 2);
    }

    public sealed class PassThroughAndSuccess : QueryValidationEndpointFilterTests {
        [Fact]
        public async Task Should_Invoke_Next_Delegate_When_QueryRequest_Passes_Validation() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest validRequest = new(
                sort: new Sort("price"),
                filters: [
                    FilterConditionNode.Equal("title", "Workstation"),
                    FilterConditionNode.GreaterThanOrEqual("price", 1500)
                ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, validRequest);
            bool nextCalled = false;

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => {
                nextCalled = true;
                return ValueTask.FromResult<object?>(Results.Ok("DataFetched"));
            });

            // Assert
            Assert.True(nextCalled);
            Ok<string> okResult = Assert.IsType<Ok<string>>(result);
            Assert.Equal("DataFetched", okResult.Value);
        }

        [Fact]
        public async Task Should_Pass_Through_When_QueryRequest_Is_Empty() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest emptyRequest = QueryRequest.Empty;

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, emptyRequest);
            bool nextCalled = false;

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => {
                nextCalled = true;
                return ValueTask.FromResult<object?>(Results.Ok("EmptySuccess"));
            });

            // Assert
            Assert.True(nextCalled);
            Ok<string> okResult = Assert.IsType<Ok<string>>(result);
            Assert.Equal("EmptySuccess", okResult.Value);
        }

        [Fact]
        public async Task Should_Pass_Through_When_No_QueryRequest_Argument_Exists_In_Context() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, 12345, "param", CancellationToken.None);
            bool nextCalled = false;

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => {
                nextCalled = true;
                return ValueTask.FromResult<object?>(Results.Ok("NoQueryRequest"));
            });

            // Assert
            Assert.True(nextCalled);
            Ok<string> okResult = Assert.IsType<Ok<string>>(result);
            Assert.Equal("NoQueryRequest", okResult.Value);
        }
    }

    public sealed class ArgumentResolution : QueryValidationEndpointFilterTests {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Should_Detect_And_Validate_QueryRequest_At_Any_Parameter_Position(int targetPosition) {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest invalidRequest = new(filters: [
                new("UnregisteredField", QueryOperator.Equal, "123")
            ]);

            object?[] arguments = [42, "test_string", CancellationToken.None];
            arguments[targetPosition] = invalidRequest;

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, arguments);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("UnregisteredField"));
        }
    }

    public sealed class FieldAndOperatorViolations : QueryValidationEndpointFilterTests {
        [Fact]
        public async Task Should_Return_ValidationProblem_When_Field_Is_Not_Filterable() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(filters: [
                FilterConditionNode.Equal("InternalTag", "Secret")
            ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("InternalTag"));
        }

        [Fact]
        public async Task Should_Return_ValidationProblem_When_Operator_Is_Disallowed_By_Bitmask() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(filters: [
                FilterConditionNode.LessThan("price", 500)
            ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("price"));
        }

        [Fact]
        public async Task Should_Return_ValidationProblem_When_Sort_Field_Is_Not_Sortable() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(sort: new Sort("Title"));

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("Title"));
        }
    }

    public sealed class ValueFormatAndRangeViolations : QueryValidationEndpointFilterTests {
        [Theory]
        [InlineData("price", "not_a_decimal")]
        [InlineData("status", "NonExistentStatusEnum")]
        [InlineData("createdAt", "invalid_iso_timestamp")]
        public async Task Should_Return_ValidationProblem_For_Malformed_Type_Values(string field, string invalidValue) {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(filters: [
                new(field, QueryOperator.Equal, invalidValue)
            ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey(field));
        }

        [Theory]
        [InlineData("100..")]
        [InlineData("..500")]
        [InlineData("invalid_range")]
        public async Task Should_Return_ValidationProblem_For_Malformed_Between_Range_Expressions(string malformedRange) {
            // Arrange
            QuerySchema<Item> schema = new QuerySchema<Item>().AllowFilter(x => x.Price);
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(filters: [
                new("price", QueryOperator.Between, malformedRange)
            ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("price"));
        }
    }

    public sealed class SecurityLimitsAndGlobalViolations : QueryValidationEndpointFilterTests {
        [Fact]
        public async Task Should_Map_Global_Filter_Count_Limit_Breach_To_Root_Key() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(filters: [
                FilterConditionNode.Equal("title", "A"),
                FilterConditionNode.Equal("title", "B"),
                FilterConditionNode.Equal("title", "C"),
                FilterConditionNode.Equal("title", "D")
            ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("$"));
        }

        [Fact]
        public async Task Should_Map_Global_Sort_Count_Limit_Breach_To_Root_Key() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(sort: new Sort("price,createdAt,title"));

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("$"));
        }

        [Fact]
        public async Task Should_Aggregate_Multiple_Distinct_Field_Errors_In_Single_Response() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            QueryRequest request = new(
                sort: new Sort("title"),
                filters: [
                    FilterConditionNode.Equal("UnknownField", "123"),
                    FilterConditionNode.LessThan("price", 100)
                ]);

            DefaultHttpContext httpContext = new();
            DefaultEndpointFilterInvocationContext filterContext = new(httpContext, request);

            // Act
            object? result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>(Results.Ok()));

            // Assert
            ProblemHttpResult problemResult = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);

            HttpValidationProblemDetails validationDetails = Assert.IsType<HttpValidationProblemDetails>(problemResult.ProblemDetails);
            Assert.True(validationDetails.Errors.ContainsKey("title"));
            Assert.True(validationDetails.Errors.ContainsKey("UnknownField"));
            Assert.True(validationDetails.Errors.ContainsKey("price"));
        }
    }

    public sealed class RouteBuilderExtensionTests : QueryValidationEndpointFilterTests {
        [Fact]
        public void WithQueryValidation_Should_Return_Same_Builder_Instance_For_Chaining() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            TestEndpointConventionBuilder builder = new();

            // Act
            TestEndpointConventionBuilder returnedBuilder = builder.WithQueryValidation(schema);

            // Assert
            Assert.Same(builder, returnedBuilder);
            Assert.Single(builder.FilterFactories);
        }
    }

    public sealed class PreconditionEnforcement : QueryValidationEndpointFilterTests {
        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_When_Schema_Is_Null() {
            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => new QueryValidationEndpointFilter<Item>(null!));
        }

        [Fact]
        public async Task InvokeAsync_Should_Throw_ArgumentNullException_When_Context_Is_Null() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);

            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                filter.InvokeAsync(null!, _ => ValueTask.FromResult<object?>(Results.Ok())).AsTask());
        }

        [Fact]
        public async Task InvokeAsync_Should_Throw_ArgumentNullException_When_Next_Delegate_Is_Null() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            QueryValidationEndpointFilter<Item> filter = new(schema);
            DefaultEndpointFilterInvocationContext filterContext = new(new DefaultHttpContext());

            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                filter.InvokeAsync(filterContext, null!).AsTask());
        }

        [Fact]
        public void WithQueryValidation_Should_Throw_ArgumentNullException_When_Builder_Or_Schema_Is_Null() {
            // Arrange
            QuerySchema<Item> schema = CreateSampleSchema();
            IEndpointConventionBuilder validBuilder = new TestEndpointConventionBuilder();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() =>
                ((IEndpointConventionBuilder)null!).WithQueryValidation(schema));

            Assert.ThrowsAny<ArgumentNullException>(() =>
                validBuilder.WithQueryValidation<IEndpointConventionBuilder, Item>(null!));
        }
    }

    private sealed class TestEndpointConventionBuilder : IEndpointConventionBuilder {
        public List<Action<EndpointBuilder>> FilterFactories { get; } = [];

        public void Add(Action<EndpointBuilder> convention) {
            this.FilterFactories.Add(convention);
        }
    }

    private sealed class DefaultEndpointFilterInvocationContext(HttpContext httpContext, params object?[] arguments) : EndpointFilterInvocationContext {
        private readonly IList<object?> _arguments = arguments.ToList();

        public override HttpContext HttpContext => httpContext;
        public override IList<object?> Arguments => this._arguments;
        public override T GetArgument<T>(int index) {
            return (T)this._arguments[index]!;
        }
    }
}