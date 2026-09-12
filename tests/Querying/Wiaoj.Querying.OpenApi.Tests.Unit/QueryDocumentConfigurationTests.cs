using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System.Text.Json;
using Wiaoj.Querying.AspNetCore;

namespace Wiaoj.Querying.OpenApi.Tests.Unit;

/// <summary>
/// The document follows the application's configuration: names, types, ignore rules and operator tokens as
/// the application enforces them — and it can be shaped without patching its output.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "OpenApi")]
public sealed class QueryDocumentConfigurationTests {

    public enum Status { Draft, Review, Approved }

    public sealed class Key {
        public int Id { get; set; }
        public string ContentType { get; set; } = "";
        public bool IsDeprecated { get; set; }
        public Status Status { get; set; }
        public long Revision { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private static OpenApiOperation Transform(
        Action<IQueryingBuilder> querying,
        Action<QueryOpenApiOptions>? document = null,
        QueryValidationEndpointOptions? endpointOptions = null) {

        ServiceCollection services = new();
        services.AddQuerying(querying);

        List<object> metadata = [new QueryValidationEndpointMetadata(typeof(Key))];
        if(endpointOptions is not null) {
            metadata.Add(endpointOptions);
        }

        OpenApiOperation operation = new() {
            Parameters = [],
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "OK" } }
        };

        OpenApiOperationTransformerContext context = new() {
            DocumentName = "v1",
            Description = new ApiDescription { ActionDescriptor = new ActionDescriptor { EndpointMetadata = metadata } },
            ApplicationServices = services.BuildServiceProvider()
        };

        QueryOpenApiOptions options = new();
        document?.Invoke(options);

        new QueryValidationOperationTransformer(options)
            .TransformAsync(operation, context, TestContext.Current.CancellationToken)
            .GetAwaiter().GetResult();

        return operation;
    }

    private static OpenApiParameter? Parameter(OpenApiOperation operation, string name) {
        return operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(p => p.Name == name);
    }

    private static void KeySchema(QuerySchema<Key> schema) {
        schema.Property(k => k.ContentType).AllowFilter(QueryOperator.Equal, QueryOperator.IsNull).AllowSort();
        schema.Property(k => k.IsDeprecated).AllowFilter(QueryOperator.Equal);
        schema.Property(k => k.Status).AllowFilter(QueryOperator.Equal, QueryOperator.In, QueryOperator.NotBetween);
        schema.Property(k => k.Revision).AllowFilter(QueryOperator.GreaterThan);
        schema.Property(k => k.UpdatedAt).AllowFilter(QueryOperator.GreaterThanOrEqual);
    }

    public sealed class Names {
        [Fact]
        public void Should_Follow_The_Configured_Naming_Policy() {
            OpenApiOperation operation = Transform(q => q
                .UseFieldNamingPolicy(JsonNamingPolicy.SnakeCaseLower)
                .AddSchema<Key>(KeySchema));

            Assert.NotNull(Parameter(operation, "content_type"));
            Assert.NotNull(Parameter(operation, "is_deprecated"));
            Assert.Null(Parameter(operation, "ContentType"));
            Assert.Contains("content_type", Parameter(operation, "sort")!.Description, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Leave_Names_As_Written_Without_A_Policy() {
            OpenApiOperation operation = Transform(q => q.AddSchema<Key>(KeySchema));

            Assert.NotNull(Parameter(operation, "ContentType"));
        }
    }

    public sealed class Types {
        [Fact]
        public void Should_Type_Each_Filter_From_Its_Field() {
            OpenApiOperation operation = Transform(q => q.AddSchema<Key>(KeySchema));

            Assert.Equal(JsonSchemaType.Boolean, Parameter(operation, "IsDeprecated")!.Schema!.Type);
            Assert.Equal("int64", Parameter(operation, "Revision")!.Schema!.Format);
            Assert.Equal("date-time", Parameter(operation, "UpdatedAt")!.Schema!.Format);
            Assert.Equal(JsonSchemaType.String, Parameter(operation, "ContentType")!.Schema!.Type);
        }

        [Fact]
        public void Should_List_Enum_Names_The_Engine_Parses() {
            OpenApiOperation operation = Transform(q => q.AddSchema<Key>(KeySchema));

            string[] values = [.. Parameter(operation, "Status")!.Schema!.Enum!.Select(node => node!.GetValue<string>())];

            Assert.Equal(["Draft", "Review", "Approved"], values);
        }
    }

    public sealed class OperatorTokens {
        [Fact]
        public void Should_Write_The_Tokens_The_Parser_Reads() {
            // The transformer's own copy of this mapping lacked these three and wrote the enum names instead.
            OpenApiOperation operation = Transform(q => q.AddSchema<Key>(KeySchema));

            Assert.Contains("isNull", Parameter(operation, "ContentType")!.Description, StringComparison.Ordinal);
            Assert.Contains("notBetween", Parameter(operation, "Status")!.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("IsNull", Parameter(operation, "ContentType")!.Description, StringComparison.Ordinal);
        }
    }

    public sealed class IgnoreRules {
        [Fact]
        public void Should_Not_Advertise_A_Globally_Ignored_Field() {
            OpenApiOperation operation = Transform(q => q
                .IgnoreParameters("Revision")
                .AddSchema<Key>(KeySchema));

            Assert.Null(Parameter(operation, "Revision"));
            Assert.NotNull(Parameter(operation, "ContentType"));
        }

        [Fact]
        public void Should_Advertise_It_Again_For_An_Endpoint_That_Opts_Out_Of_Global_Rules() {
            QueryValidationEndpointOptions endpoint = new();
            endpoint.IgnoreGlobalParameters();

            OpenApiOperation operation = Transform(
                q => q.IgnoreParameters("Revision").AddSchema<Key>(KeySchema),
                endpointOptions: endpoint);

            Assert.NotNull(Parameter(operation, "Revision"));
        }
    }

    public sealed class DeepObjectStyle {
        [Fact]
        public void Should_Describe_Each_Filter_As_An_Object_Of_Its_Operators() {
            OpenApiOperation operation = Transform(
                q => q.AddSchema<Key>(KeySchema),
                document => document.FilterStyle = QueryFilterStyle.DeepObject);

            OpenApiParameter status = Parameter(operation, "Status")!;

            Assert.Equal(ParameterStyle.DeepObject, status.Style);
            Assert.True(status.Explode);
            Assert.Equal(["eq", "in", "notBetween"], status.Schema!.Properties!.Keys);
        }

        [Fact]
        public void Should_Type_Each_Operator() {
            OpenApiOperation operation = Transform(
                q => q.AddSchema<Key>(KeySchema),
                document => document.FilterStyle = QueryFilterStyle.DeepObject);

            IDictionary<string, IOpenApiSchema> revision = Parameter(operation, "Revision")!.Schema!.Properties!;
            IDictionary<string, IOpenApiSchema> content = Parameter(operation, "ContentType")!.Schema!.Properties!;

            Assert.Equal(JsonSchemaType.Integer, revision["gt"].Type);
            Assert.Equal(JsonSchemaType.Boolean, content["isNull"].Type);
        }
    }

    public sealed class Hooks {
        [Fact]
        public void Should_Run_Per_Filter_With_The_Field_It_Describes() {
            List<string> seen = [];

            OpenApiOperation operation = Transform(
                q => q.AddSchema<Key>(KeySchema),
                document => document.ConfigureFilter = (field, parameter) => {
                    seen.Add(field.Name);
                    parameter.Deprecated = field.Name == "Revision";
                });

            Assert.Contains("Status", seen);
            Assert.True(Parameter(operation, "Revision")!.Deprecated);
        }

        [Fact]
        public void Should_Run_Once_Per_Operation_With_Every_Field() {
            int calls = 0;
            int fields = 0;

            Transform(
                q => q.AddSchema<Key>(KeySchema),
                document => document.ConfigureOperation = (_, descriptors) => {
                    calls++;
                    fields = descriptors.Count;
                });

            Assert.Equal(1, calls);
            Assert.Equal(5, fields);
        }
    }

    public sealed class CustomFilters {
        [Fact]
        public void Should_Be_Documented_With_Their_Description_And_Type() {
            OpenApiOperation operation = Transform(q => q.AddSchema<Key>(schema => {
                KeySchema(schema);
                schema.CustomFilter<bool>("hasScreenshot")
                    .AllowFilter(QueryOperator.Equal)
                    .Describe("Keys with at least one screenshot.");
            }));

            OpenApiParameter parameter = Parameter(operation, "hasScreenshot")!;

            Assert.Equal(JsonSchemaType.Boolean, parameter.Schema!.Type);
            Assert.StartsWith("Keys with at least one screenshot.", parameter.Description, StringComparison.Ordinal);
        }
    }
}
