using Microsoft.AspNetCore.Mvc.ApiExplorer;
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
/// handler taking <c>[AsParameters] PageRequest</c> has them described already, and duplicating a parameter
/// produces an invalid document. A handler taking <c>CursorParameters</c> has none of them described,
/// because a type that binds itself contributes nothing to the document. Which set applies is decided by the
/// declared response type, not guessed: <c>PagedResult&lt;T&gt;</c> means offset paging,
/// <c>CursorResult&lt;T&gt;</c> means keyset.
/// </para>
/// <para>
/// A handler taking a bare <c>CursorRequest</c> or <c>PageRequest</c> is a third case: it binds the whole
/// request from one query value in a compact format, and accepts none of the separate parameters. That one
/// gets its grammar written down instead.
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

        // An endpoint that declared where its metadata is — an envelope response — has stated its style;
        // otherwise it is read from the declared response type.
        PagingStyle style = metadata.Style switch {
            PaginationStyle.Offset => PagingStyle.Offset,
            PaginationStyle.Cursor => PagingStyle.Cursor,
            _ => DetectStyle(context)
        };

        // The filter acts on a page and leaves anything else alone, so an endpoint carrying the marker but
        // returning neither shape gets no Link header, no ETag and no 304. Describing them anyway would put
        // headers in the document that the endpoint never sends.
        if(style == PagingStyle.Unknown) {
            return Task.CompletedTask;
        }

        // Resolved by the same method, from the same instance, the filter uses at run time — so the document
        // cannot claim an ETag the application configured off.
        PaginationOptions options = metadata.Resolve(context.ApplicationServices);

        DescribeParameters(operation, context, style);
        DescribeResponseHeaders(operation, options);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds the paging query parameters for whichever style the endpoint returns, skipping any the document
    /// already describes.
    /// </summary>
    private static void DescribeParameters(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        PagingStyle style) {

        if(DescribeCompactRequest(operation, context)) {
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
    /// Describes a request record bound as one value, and reports whether it found one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CursorRequest"/> and <see cref="PageRequest"/> are both <c>ISpanParsable</c>, so a handler
    /// can take one directly and receive it from a single query value. The document then carries a bare
    /// <c>string</c> with nothing to say what belongs in it, and a caller finds the grammar by trial.
    /// </para>
    /// <para>
    /// An endpoint bound this way also does <b>not</b> accept the separate paging parameters, so finding one
    /// is what stops this transformer from adding them — documenting a <c>cursor</c> query parameter the
    /// endpoint ignores is worse than documenting nothing.
    /// </para>
    /// </remarks>
    private static bool DescribeCompactRequest(OpenApiOperation operation, OpenApiOperationTransformerContext context) {
        bool found = false;

        foreach(ApiParameterDescription parameter in context.Description.ParameterDescriptions) {
            string? grammar = Grammar(parameter.Type);

            if(grammar is null) {
                continue;
            }

            found = true;

            OpenApiParameter? documented = operation.Parameters?
                .OfType<OpenApiParameter>()
                .FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.Ordinal));

            if(documented is not null && string.IsNullOrEmpty(documented.Description)) {
                documented.Description = grammar;
            }
        }

        return found;
    }

    private static string? Grammar(Type? type) {
        if(type == typeof(CursorRequest)) {
            return "Keyset paging request, as cursor:limit:direction. The limit and direction may be omitted, "
                + "so a bare cursor and cursor:limit both parse. Limit defaults to "
                + $"{CursorRequest.DefaultLimit} and is capped at {CursorRequest.MaxLimit}; direction is "
                + "Forward or Backward, and defaults to Forward.";
        }

        if(type == typeof(PageRequest)) {
            return "Offset paging request, as page:size. Page is 1-based; size defaults to "
                + $"{PageRequest.DefaultSize} and is capped at {PageRequest.MaxSize}.";
        }

        return null;
    }

    /// <summary>
    /// Records the headers the filter writes, and the 304 it can answer.
    /// </summary>
    private static void DescribeResponseHeaders(OpenApiOperation operation, PaginationOptions options) {
        if(operation.Responses is null) {
            return;
        }

        foreach(KeyValuePair<string, IOpenApiResponse> entry in operation.Responses) {
            if(!entry.Key.StartsWith('2') || entry.Value is not OpenApiResponse response) {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);

            if(options.EnableLinkHeaders) {
                response.Headers.TryAdd(LinkHeader, new OpenApiHeader {
                    Description = "RFC 8288 links to the first, previous, next and last pages, as applicable.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }

            if(options.EnableETag) {
                response.Headers.TryAdd(ETagHeader, new OpenApiHeader {
                    Description = "Entity tag for the page. Send it back as If-None-Match to receive 304.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
        }

        if(options.EnableETag) {
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

            // The runtime filter matches on these interfaces, so the document has to as well: a response type
            // implementing one is paginated whether or not it is one of the library's own generic results.
            if(type is not null && typeof(IPagedResult).IsAssignableFrom(type)) {
                return PagingStyle.Offset;
            }

            if(type is not null && typeof(ICursorResult).IsAssignableFrom(type)) {
                return PagingStyle.Cursor;
            }

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
