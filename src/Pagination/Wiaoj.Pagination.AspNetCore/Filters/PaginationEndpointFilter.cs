using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Wiaoj.Pagination;
using Wiaoj.Pagination.AspNetCore.Caching;
using Wiaoj.Pagination.AspNetCore.Linking;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Pagination.AspNetCore.Filters;

/// <summary>
/// An endpoint filter that automatically appends RFC 8288 <c>Link</c> headers,
/// <c>ETag</c> headers, and evaluates conditional <c>304 Not Modified</c> requests for paginated results.
/// </summary>
internal sealed class PaginationEndpointFilter : IEndpointFilter {

    /// <summary>
    /// A shared instance for endpoints with no configuration of their own. It carries no settings, so sharing
    /// it across endpoints and applications is safe: every request resolves the application's options.
    /// </summary>
    public static readonly PaginationEndpointFilter Default = new(new PaginationEndpointMetadata(configure: null));

    private readonly PaginationEndpointMetadata _metadata;

    /// <summary>Gets the metadata this filter reads its settings from.</summary>
    internal PaginationEndpointMetadata Metadata => this._metadata;

    /// <summary>
    /// Initializes a filter that reads its settings from <paramref name="metadata"/> — the same instance the
    /// OpenAPI transformer reads, so the document and the behaviour cannot disagree.
    /// </summary>
    public PaginationEndpointFilter(PaginationEndpointMetadata metadata) {
        Preca.ThrowIfNull(metadata);
        this._metadata = metadata;
    }

    /// <summary>Initializes a filter fixed to <paramref name="options"/>.</summary>
    public PaginationEndpointFilter(PaginationOptions options) : this(new PaginationEndpointMetadata(options)) { }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        object? result = await next(context).ConfigureAwait(false);

        if(result is null) {
            return null;
        }

        HttpContext httpContext = context.HttpContext;
        object? value = GetValueFromResult(result);

        if(value is null) {
            return result;
        }

        PaginationOptions options = this._metadata.Resolve(httpContext.RequestServices);

        // Everything below this line is about a page. A result that is not one leaves untouched — including
        // its ETag, which is what made the unwrapping defect silent: an unreadable result still got an ETag,
        // computed over a serialisation carrying none of its data, identical on every response.
        if(!TryApplyPageHeaders(httpContext, value, options)) {
            return result;
        }

        if(options.EnableETag && httpContext.Response.StatusCode is 0 or 200) {
            JsonSerializerOptions? jsonOptions = httpContext.RequestServices?
                .GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?
                .Value.SerializerOptions;

            XxHash3 hash = XxHash3.Compute((value, jsonOptions), static (writer, val) => {
                using Utf8JsonWriter jsonWriter = new(writer);
                JsonSerializer.Serialize(jsonWriter, val.value, val.jsonOptions);
            });

            string etag = ETagGenerator.FormatWeakETag(hash);
            httpContext.Response.Headers.ETag = etag;

            if(ETagGenerator.IsNotModified(httpContext.Request.Headers.IfNoneMatch, etag)) {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
        }

        return result;
    }

    /// <summary>
    /// Writes the <c>Link</c> headers for whichever page shape this is, and reports whether it was one.
    /// </summary>
    /// <param name="httpContext">The request being answered.</param>
    /// <param name="value">The handler's unwrapped return value.</param>
    /// <param name="options">The options in effect for this request.</param>
    /// <returns><see langword="true"/> when the value was a page; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Matched through the non-generic interfaces rather than reached through <c>dynamic</c>. The runtime
    /// binder resolves members against the calling assembly's view of the type, so a
    /// <c>CursorResult&lt;T&gt;</c> whose <c>T</c> is <c>internal</c> to the application binds against
    /// <see cref="ValueType"/> — which has no <c>Metadata</c> — and throws at run time. An internal response
    /// DTO is the ordinary case, so that was every such endpoint.
    /// <para>
    /// An envelope — a response carrying a page alongside other data — is read through the accessor the
    /// endpoint declared with <c>WithPagination&lt;TResponse&gt;(r =&gt; r.Metadata)</c>, so the response type
    /// does not have to know this library exists.
    /// </para>
    /// </remarks>
    private bool TryApplyPageHeaders(HttpContext httpContext, object value, PaginationOptions options) {
        object? metadata = value switch {
            IPagedResult page => page.Metadata,
            ICursorResult window => window.Metadata,
            _ => this._metadata.ReadMetadata?.Invoke(value)
        };

        switch(metadata) {
            case PageMetadata page:
                ApplyOffsetHeaders(httpContext, page, options);
                return true;

            case CursorMetadata window:
                ApplyCursorHeaders(httpContext, window, options);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Digs the handler's return value out of the result it was wrapped in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A handler declaring <c>Results&lt;Ok&lt;CursorResult&lt;T&gt;&gt;, ProblemHttpResult&gt;</c> returns a
    /// <see cref="INestedHttpResult"/>, not an <see cref="IValueHttpResult"/> — the value sits one level
    /// further in. Unwrapping only the latter left this filter holding the union wrapper, which is neither a
    /// page nor null, so it wrote no <c>Link</c> header and computed the ETag over a serialisation of the
    /// wrapper: the same handful of bytes for every response, on every endpoint written that way.
    /// </para>
    /// <para>
    /// A constant ETag matches every <c>If-None-Match</c>, so the second request for a page that had changed
    /// was answered <c>304</c> with the client's stale copy left in place.
    /// </para>
    /// </remarks>
    private static object? GetValueFromResult(object result) {
        // Bounded rather than while(true): the nesting is a handful of levels in practice, and a result type
        // that returned itself would otherwise spin here.
        for(int depth = 0; depth < 8; depth++) {
            switch(result) {
                case IValueHttpResult valueResult:
                    return valueResult.Value;

                case INestedHttpResult nested when nested.Result is not null:
                    result = nested.Result;
                    continue;

                default:
                    return result;
            }
        }

        return result;
    }

    private static void ApplyOffsetHeaders(HttpContext httpContext, PageMetadata metadata, PaginationOptions options) {
        if(metadata.IsEmpty || !options.EnableLinkHeaders) return;

        PathString path = httpContext.Request.Path;
        IQueryCollection query = httpContext.Request.Query;

        string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, page =>
            BuildOffsetUri(path, query, page));

        if(!string.IsNullOrEmpty(linkHeader)) {
            httpContext.Response.Headers.Link = linkHeader;
        }
    }

    private static void ApplyCursorHeaders(HttpContext httpContext, CursorMetadata metadata, PaginationOptions options) {
        if(metadata.IsEmpty || !options.EnableLinkHeaders) return;

        PathString path = httpContext.Request.Path;
        IQueryCollection query = httpContext.Request.Query;

        string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, (cursor, direction) =>
            BuildCursorUri(path, query, cursor, direction));

        if(!string.IsNullOrEmpty(linkHeader)) {
            httpContext.Response.Headers.Link = linkHeader;
        }
    }

    private static string BuildOffsetUri(PathString path, IQueryCollection query, int pageNumber) {
        if(query.Count == 0 || (query.Count == 1 && query.ContainsKey(PaginationParameters.Page))) {
            return $"{path}?{PaginationParameters.Page}={pageNumber}";
        }

        StringBuilder sb = new(path.Value?.Length + 32 ?? 32);
        sb.Append(path.Value);
        char separator = '?';

        foreach(KeyValuePair<string, StringValues> pair in query) {
            if(string.Equals(pair.Key, PaginationParameters.Page, StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append(separator).Append(pair.Key).Append('=').Append(pair.Value);
            separator = '&';
        }

        sb.Append(separator).Append(PaginationParameters.Page).Append('=').Append(pageNumber);
        return sb.ToString();
    }

    private static string BuildCursorUri(PathString path, IQueryCollection query, CursorToken cursor, CursorDirection direction) {
        StringBuilder sb = new(path.Value?.Length + 64 ?? 64);
        sb.Append(path.Value);
        char separator = '?';

        foreach(KeyValuePair<string, StringValues> pair in query) {
            if(string.Equals(pair.Key, PaginationParameters.Cursor, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(pair.Key, PaginationParameters.Direction, StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append(separator).Append(pair.Key).Append('=').Append(pair.Value);
            separator = '&';
        }

        sb.Append(separator).Append(PaginationParameters.Cursor).Append('=').Append(cursor.Value)
          .Append('&').Append(PaginationParameters.Direction).Append('=').Append(direction);

        return sb.ToString();
    }
}