using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.OpenApi.Tests.Unit;

/// <summary>
/// A POST query endpoint reads its query from the body; the document describes that body, the statuses the binder
/// returns for it, and — where QUERY is accepted — the Accept-Query response header (#145).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "OpenApi")]
public sealed class QueryRequestBodyDocumentationTests {
    private sealed class Product {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private sealed class AcmeParser : IQueryPayloadParser {
        public IReadOnlyList<string> SupportedMediaTypes { get; } = ["application/vnd.acme.query"];
        public bool CanParse(string mediaType) => false;
        public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
            result = QueryRequest.Empty;
            return false;
        }
    }

    private static QuerySchema<Product> Schema() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter(QueryOperator.Equal, QueryOperator.Contains).AllowSort();
        schema.Property(p => p.Price).AllowFilter(QueryOperator.GreaterThanOrEqual, QueryOperator.In);
        schema.ConfigureLimits(maxFilters: 4, maxInValues: 10, maxSortFields: 2, maxFilterValueLength: 100, maxSearchTermLength: 64);
        return schema;
    }

    private static OpenApiOperation Transform(
        string httpMethod,
        string[] endpointMethods,
        QuerySchema<Product>? schema = null,
        QueryOpenApiOptions? options = null,
        Action<IServiceCollection>? services = null) {

        ServiceCollection collection = new();
        collection.AddSingleton(schema ?? Schema());
        services?.Invoke(collection);

        ApiDescription description = new() {
            HttpMethod = httpMethod,
            ActionDescriptor = new ActionDescriptor {
                EndpointMetadata = [new QueryValidationEndpointMetadata(typeof(Product)), new HttpMethodMetadata(endpointMethods)]
            }
        };

        OpenApiOperation operation = new() {
            Parameters = [],
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "OK" } }
        };

        new QueryValidationOperationTransformer(options ?? new QueryOpenApiOptions())
            .TransformAsync(operation, new OpenApiOperationTransformerContext {
                DocumentName = "v1",
                Description = description,
                ApplicationServices = collection.BuildServiceProvider()
            }, TestContext.Current.CancellationToken)
            .GetAwaiter().GetResult();

        return operation;
    }

    private static OpenApiSchema Json(OpenApiOperation operation) {
        OpenApiRequestBody body = Assert.IsType<OpenApiRequestBody>(operation.RequestBody);
        return Assert.IsType<OpenApiSchema>(body.Content!["application/json"].Schema);
    }

    public sealed class TheBody {
        [Fact]
        public void Should_Describe_An_Optional_Body_With_Every_Registered_Media_Type() {
            OpenApiOperation operation = Transform("POST", ["POST"], services: s => {
                s.AddSingleton<IQueryPayloadParser, JsonQueryPayloadParser>();
                s.AddSingleton<IQueryPayloadParser, BracketQueryPayloadParser>();
                s.AddSingleton<IQueryPayloadParser, AcmeParser>();
            });

            OpenApiRequestBody body = Assert.IsType<OpenApiRequestBody>(operation.RequestBody);
            Assert.False(body.Required);
            Assert.Equal(
                ["application/json", "text/plain", "application/x-www-form-urlencoded", "application/vnd.acme.query"],
                body.Content!.Keys);
        }

        [Fact]
        public void Should_Fall_Back_To_The_Built_In_Media_Types_When_No_Parser_Is_Registered() {
            OpenApiRequestBody body = Assert.IsType<OpenApiRequestBody>(Transform("POST", ["POST"]).RequestBody);

            Assert.Equal(["application/json", "text/plain", "application/x-www-form-urlencoded"], body.Content!.Keys);
        }

        [Fact]
        public void Should_Describe_The_Json_Payload_With_The_Schema_Limits() {
            OpenApiSchema json = Json(Transform("POST", ["POST"]));

            Assert.Equal(JsonSchemaType.Object, json.Type);
            Assert.Equal(64, Assert.IsType<OpenApiSchema>(json.Properties!["q"]).MaxLength);
            Assert.Contains("Name", Assert.IsType<OpenApiSchema>(json.Properties["sort"]).Description, StringComparison.Ordinal);

            OpenApiSchema filters = Assert.IsType<OpenApiSchema>(json.Properties["filters"]);
            Assert.Equal(JsonSchemaType.Array, filters.Type);
            Assert.Equal(4, filters.MaxItems);
            Assert.Equal(2, Assert.IsType<OpenApiSchema>(filters.Items).OneOf!.Count);
        }

        [Fact]
        public void Should_Pair_Each_Field_Only_With_The_Operators_It_Permits() {
            OpenApiSchema filters = Assert.IsType<OpenApiSchema>(Json(Transform("POST", ["POST"])).Properties!["filters"]);
            OpenApiSchema[] alternatives = [.. Assert.IsType<OpenApiSchema>(filters.Items).OneOf!.Cast<OpenApiSchema>()];

            OpenApiSchema price = alternatives.Single(a => a.Properties!["field"].Enum!.Single()!.GetValue<string>() == "Price");
            Assert.Equal(["gte", "in"], price.Properties!["op"].Enum!.Select(e => e!.GetValue<string>()));
            // Price does not permit equality, so op cannot be omitted.
            Assert.Contains("op", price.Required!);
            // gte is typed as the number, in as the comma-separated string.
            Assert.Equal(2, Assert.IsType<OpenApiSchema>(price.Properties["value"]).AnyOf!.Count);

            OpenApiSchema name = alternatives.Single(a => a.Properties!["field"].Enum!.Single()!.GetValue<string>() == "Name");
            Assert.DoesNotContain("op", name.Required!);
        }

        [Fact]
        public void Should_Leave_Out_Filters_And_Sort_When_Their_Limit_Is_Zero() {
            QuerySchema<Product> schema = Schema();
            schema.ConfigureLimits(maxFilters: 0, maxInValues: 10, maxSortFields: 0);

            OpenApiSchema json = Json(Transform("POST", ["POST"], schema));

            Assert.False(json.Properties!.ContainsKey("filters"));
            Assert.False(json.Properties.ContainsKey("sort"));
            Assert.True(json.Properties.ContainsKey("q"));
        }

        [Fact]
        public void Should_Leave_Out_A_Field_The_Endpoint_Ignores() {
            QuerySchema<Product> schema = Schema();
            schema.IgnoreParameters(nameof(Product.Price));

            OpenApiSchema filters = Assert.IsType<OpenApiSchema>(Json(Transform("POST", ["POST"], schema)).Properties!["filters"]);

            Assert.Single(Assert.IsType<OpenApiSchema>(filters.Items).OneOf!);
        }

        [Fact]
        public void Should_Keep_The_Query_Parameters_By_Default() {
            OpenApiOperation operation = Transform("POST", ["POST"]);

            Assert.Contains(operation.Parameters!, p => p.Name == "Name");
        }

        [Fact]
        public void Should_Describe_Only_The_Body_When_Asked() {
            OpenApiOperation operation = Transform("POST", ["POST"], options: new QueryOpenApiOptions {
                RequestBodyDescription = QueryRequestBodyDescription.BodyOnly
            });

            Assert.Empty(operation.Parameters!);
            Assert.NotNull(operation.RequestBody);
        }

        [Fact]
        public void Should_Document_The_Statuses_The_Binder_Returns_For_A_Body() {
            OpenApiOperation operation = Transform("POST", ["POST"]);

            Assert.True(operation.Responses!.ContainsKey("413"));
            OpenApiResponse unsupported = Assert.IsType<OpenApiResponse>(operation.Responses["415"]);
            Assert.True(unsupported.Headers!.ContainsKey("Accept"));
        }

        [Fact]
        public void Should_Leave_A_GET_Operation_Without_A_Body() {
            OpenApiOperation operation = Transform("GET", ["GET"]);

            Assert.Null(operation.RequestBody);
            Assert.False(operation.Responses!.ContainsKey("413"));
            Assert.False(operation.Responses.ContainsKey("415"));
        }
    }

    public sealed class TheAcceptQueryHeader {
        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        public void Should_Document_Accept_Query_On_Every_Response_Of_An_Endpoint_That_Accepts_QUERY(string httpMethod) {
            OpenApiOperation operation = Transform(httpMethod, ["GET", "QUERY", "POST"]);

            foreach(IOpenApiResponse response in operation.Responses!.Values) {
                Assert.True(Assert.IsType<OpenApiResponse>(response).Headers?.ContainsKey("Accept-Query"));
            }
        }

        [Fact]
        public void Should_Not_Document_Accept_Query_Where_QUERY_Is_Not_Accepted() {
            OpenApiOperation operation = Transform("POST", ["POST"]);

            Assert.All(operation.Responses!.Values, response =>
                Assert.False(Assert.IsType<OpenApiResponse>(response).Headers?.ContainsKey("Accept-Query") == true));
        }
    }
}
