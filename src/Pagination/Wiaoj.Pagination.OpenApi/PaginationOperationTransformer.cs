using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Wiaoj.Pagination.AspNetCore;

namespace Wiaoj.Pagination.OpenApi;

/// <summary>
/// Documents what <c>WithPagination()</c> actually does to an endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The filter's contribution is invisible to document generation: it writes <c>Link</c> and <c>ETag</c>
/// response headers and can answer <c>304</c>, none of which appears anywhere in the handler's signature.
/// This reads the endpoint metadata left by <c>WithPagination()</c> and writes those down.
/// </para>
/// <para>
/// It also documents the paging query parameters, but only those the document does not already carry — a
/// handler taking <c>[AsParameters] CursorRequest</c> has them described already, and duplicating a
/// parameter produces an invalid document. Which set applies is decided by the declared response type, not
/// guessed: <c>PagedResult&lt;T&gt;</c> means offset paging, <c>CursorResult&lt;T&gt;</c> means keyset.
/// </para>
/// </remarks>
internal sealed class PaginationOperationTransformer : IOpenApiOperationTransformer {
    private const string LinkHeader = "Link";
    private const string ETagHeader = "ETag";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {

        PaginationEndpointMetadata? metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<PaginationEndpointMetadata>()
            .LastOrDefault();

        if(metadata is null) {
            return Task.CompletedTask;
        }

        DescribeParameters(operation, context);
        DescribeResponseHeaders(operation, metadata);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds the paging query parameters for whichever style the endpoint returns, skipping any the document
    /// already describes.
    /// </summary>
    private static void DescribeParameters(OpenApiOperation operation, OpenApiOperationTransformerContext context) {
        PagingStyle style = DetectStyle(context);

        if(style == PagingStyle.Unknown) {
            return;
        }

        operation.Parameters ??= [];

        if(style == PagingStyle.Offset) {
            AddIfMissing(operation, PaginationParameters.Page, "integer", "int32",
                $"1-based page number. Defaults to 1.");
            AddIfMissing(operation, PaginationParameters.Size, "integer", "int32",
                $"Items per page. Defaults to {PageRequest.DefaultSize}, capped at {PageRequest.MaxSize}.");
            return;
        }

        AddIfMissing(operation, PaginationParameters.Cursor, "string", format: null,
            "Opaque cursor from a previous response's Link header. Omit for the first page.");
        AddIfMissing(operation, PaginationParameters.Limit, "integer", "int32",
            $"Items per page. Defaults to {CursorRequest.DefaultLimit}, capped at {CursorRequest.MaxLimit}.");
        AddIfMissing(operation, PaginationParameters.Direction, "string", format: null,
            "Seek direction relative to the cursor: Forward or Backward. Defaults to Forward.");
    }

    /// <summary>
    /// Records the headers the filter writes, and the 304 it can answer.
    /// </summary>
    private static void DescribeResponseHeaders(OpenApiOperation operation, PaginationEndpointMetadata metadata) {
        if(operation.Responses is null) {
            return;
        }

        foreach(KeyValuePair<string, IOpenApiResponse> entry in operation.Responses) {
            if(!entry.Key.StartsWith('2') || entry.Value is not OpenApiResponse response) {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);

            if(metadata.EmitsLinkHeaders) {
                response.Headers.TryAdd(LinkHeader, new OpenApiHeader {
                    Description = "RFC 8288 links to the first, previous, next and last pages, as applicable.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }

            if(metadata.EvaluatesETag) {
                response.Headers.TryAdd(ETagHeader, new OpenApiHeader {
                    Description = "Entity tag for the page. Send it back as If-None-Match to receive 304.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
        }

        if(metadata.EvaluatesETag) {
            operation.Responses.TryAdd("304", new OpenApiResponse {
                Description = "The page is unchanged since the ETag supplied in If-None-Match."
            });
        }
    }

    private static void AddIfMissing(OpenApiOperation operation, string name, string type, string? format, string description) {
        operation.Parameters ??= [];

        if(operation.Parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema {
                Type = type == "integer" ? JsonSchemaType.Integer : JsonSchemaType.String,
                Format = format
            }
        });
    }

    /// <summary>
    /// Decides which paging style an endpoint uses from the type it declares it returns.
    /// </summary>
    private static PagingStyle DetectStyle(OpenApiOperationTransformerContext context) {
        foreach(var responseType in context.Description.SupportedResponseTypes) {
            Type? type = responseType.Type;

            while(type is not null) {
                if(type.IsGenericType) {
                    Type definition = type.GetGenericTypeDefinition();

                    if(definition == typeof(PagedResult<>)) {
                        return PagingStyle.Offset;
                    }

                    if(definition == typeof(CursorResult<>)) {
                        return PagingStyle.Cursor;
                    }
                }

                type = type.BaseType;
            }
        }

        return PagingStyle.Unknown;
    }

    private enum PagingStyle {
        Unknown,
        Offset,
        Cursor
    }
}
