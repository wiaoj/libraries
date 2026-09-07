using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Wiaoj.Pagination.AspNetCore;
using Wiaoj.Pagination.OpenApi;

namespace Wiaoj.Pagination.OpenApi.Tests.Unit;

/// <summary>
/// The pagination filter's whole contribution — Link and ETag headers, and the 304 it can answer — is
/// invisible in a handler's signature. These pin that the generated document describes the endpoint that
/// exists rather than the one the signature implies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "OpenApi")]
public sealed class PaginationOperationTransformerTests {

    private sealed record Product(int Id);

    private static OpenApiOperation Transform(
        PaginationEndpointMetadata? metadata,
        Type? responseType,
        params OpenApiParameter[] existingParameters) {

        ApiDescription description = new() {
            ActionDescriptor = new ActionDescriptor {
                EndpointMetadata = metadata is null ? [] : [metadata]
            }
        };

        if(responseType is not null) {
            description.SupportedResponseTypes.Add(new ApiResponseType { Type = responseType, StatusCode = 200 });
        }

        OpenApiOperation operation = new() {
            Parameters = [.. existingParameters],
            Responses = new OpenApiResponses {
                ["200"] = new OpenApiResponse { Description = "OK" }
            }
        };

        OpenApiOperationTransformerContext context = new() {
            DocumentName = "v1",
            Description = description,
            ApplicationServices = null!
        };

        new PaginationOperationTransformer()
            .TransformAsync(operation, context, TestContext.Current.CancellationToken)
            .GetAwaiter().GetResult();

        return operation;
    }

    private static bool HasParameter(OpenApiOperation operation, string name) {
        return operation.Parameters?.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static IOpenApiHeader? Header(OpenApiOperation operation, string status, string header) {
        return operation.Responses?[status] is OpenApiResponse response && response.Headers?.TryGetValue(header, out IOpenApiHeader? value) == true
            ? value
            : null;
    }

    public sealed class TheResponseContract {
        [Fact]
        public void Should_Document_The_Link_And_ETag_Headers_The_Filter_Writes() {
            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(PagedResult<Product>));

            Assert.NotNull(Header(operation, "200", "Link"));
            Assert.NotNull(Header(operation, "200", "ETag"));
        }

        [Fact]
        public void Should_Document_The_304_The_Filter_Can_Answer() {
            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(PagedResult<Product>));

            Assert.True(operation.Responses!.ContainsKey("304"));
        }

        [Fact]
        public void Should_Document_Nothing_The_Endpoint_Was_Configured_Not_To_Do() {
            PaginationOptions options = new() { EnableETag = false, EnableLinkHeaders = false };

            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(options),
                typeof(PagedResult<Product>));

            Assert.Null(Header(operation, "200", "Link"));
            Assert.Null(Header(operation, "200", "ETag"));
            Assert.False(operation.Responses!.ContainsKey("304"));
        }
    }

    public sealed class TheParameters {
        [Fact]
        public void Should_Document_Offset_Parameters_For_An_Endpoint_Returning_A_Page() {
            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(PagedResult<Product>));

            Assert.True(HasParameter(operation, PaginationParameters.Page));
            Assert.True(HasParameter(operation, PaginationParameters.Size));
            Assert.False(HasParameter(operation, PaginationParameters.Cursor));
        }

        [Fact]
        public void Should_Document_Keyset_Parameters_For_An_Endpoint_Returning_A_Cursor_Result() {
            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(CursorResult<Product>));

            Assert.True(HasParameter(operation, PaginationParameters.Cursor));
            Assert.True(HasParameter(operation, PaginationParameters.Limit));
            Assert.True(HasParameter(operation, PaginationParameters.Direction));
            Assert.False(HasParameter(operation, PaginationParameters.Page));
        }

        [Fact]
        public void Should_Not_Duplicate_A_Parameter_The_Document_Already_Describes() {
            // A parameter ASP.NET Core has already described must not be added again; two parameters with the
            // same name and location make the document invalid.
            OpenApiParameter existing = new() { Name = "cursor", In = ParameterLocation.Query };

            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(CursorResult<Product>),
                existing);

            Assert.Single(operation.Parameters!, p => string.Equals(p.Name, "cursor", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Should_Invent_No_Parameters_For_An_Endpoint_Returning_Neither_Shape() {
            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(Product));

            Assert.Empty(operation.Parameters!);

            // The headers still apply: the filter runs regardless of what the handler returns.
            Assert.NotNull(Header(operation, "200", "Link"));
        }

        [Fact]
        public void Should_State_The_Limits_The_Code_Actually_Enforces() {
            OpenApiOperation operation = Transform(
                new PaginationEndpointMetadata(new PaginationOptions()),
                typeof(CursorResult<Product>));

            OpenApiParameter limit = (OpenApiParameter)operation.Parameters!
                .Single(p => string.Equals(p.Name, PaginationParameters.Limit, StringComparison.OrdinalIgnoreCase));

            Assert.Contains(CursorRequest.MaxLimit.ToString(), limit.Description, StringComparison.Ordinal);
            Assert.Contains(CursorRequest.DefaultLimit.ToString(), limit.Description, StringComparison.Ordinal);
        }
    }

    public sealed class TheUnpaginatedEndpoint {
        [Fact]
        public void Should_Be_Left_Untouched() {
            OpenApiOperation operation = Transform(metadata: null, typeof(PagedResult<Product>));

            Assert.Empty(operation.Parameters!);
            Assert.False(operation.Responses!.ContainsKey("304"));
            Assert.Null(Header(operation, "200", "Link"));
        }
    }
}
