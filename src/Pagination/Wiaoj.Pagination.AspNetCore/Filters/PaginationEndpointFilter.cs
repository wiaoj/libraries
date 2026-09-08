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
    /// A shared, pre-allocated default instance of <see cref="PaginationEndpointFilter"/> with default options.
    /// </summary>
    public static readonly PaginationEndpointFilter Default = new();

    private readonly PaginationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaginationEndpointFilter"/> class with default options.
    /// </summary>
    public PaginationEndpointFilter() : this(new PaginationOptions()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaginationEndpointFilter"/> class with specified options.
    /// </summary>
    /// <param name="options">The custom pagination options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public PaginationEndpointFilter(PaginationOptions options) {
        Preca.ThrowIfNull(options);
        this._options = options;
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        object? result = await next(context).ConfigureAwait(false);

        if(result is null) {
            return null;
        }

        HttpContext httpContext = context.HttpContext;
        object? value = GetValueFromResult(result);

        // Everything below this line is about a page. A result that is not one leaves untouched — including
        // its ETag, which is what made the unwrapping defect silent: an unreadable result still got an ETag,
        // computed over a serialisation carrying none of its data, identical on every response.
        if(value is null || !TryApplyPageHeaders(httpContext, value)) {
            return result;
        }

        if(this._options.EnableETag && httpContext.Response.StatusCode is 0 or 200) {
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
    /// <returns><see langword="true"/> when the value was a page; otherwise <see langword="false"/>.</returns>
    private bool TryApplyPageHeaders(HttpContext httpContext, object value) {
        Type valueType = value.GetType();

        if(!valueType.IsGenericType) {
            return false;
        }

        Type definition = valueType.GetGenericTypeDefinition();

        if(definition == typeof(PagedResult<>)) {
            dynamic pagedResult = value;
            ApplyOffsetHeaders(httpContext, (PageMetadata)pagedResult.Metadata);
            return true;
        }

        if(definition == typeof(CursorResult<>)) {
            dynamic cursorResult = value;
            ApplyCursorHeaders(httpContext, (CursorMetadata)cursorResult.Metadata);
            return true;
        }

        return false;
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

    private void ApplyOffsetHeaders(HttpContext httpContext, PageMetadata metadata) {
        if(metadata.IsEmpty || !this._options.EnableLinkHeaders) return;

        PathString path = httpContext.Request.Path;
        IQueryCollection query = httpContext.Request.Query;

        string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, page =>
            BuildOffsetUri(path, query, page));

        if(!string.IsNullOrEmpty(linkHeader)) {
            httpContext.Response.Headers.Link = linkHeader;
        }
    }

    private void ApplyCursorHeaders(HttpContext httpContext, CursorMetadata metadata) {
        if(metadata.IsEmpty || !this._options.EnableLinkHeaders) return;

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