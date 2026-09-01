using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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

        if(value is null) {
            return result;
        }

        Type valueType = value.GetType();

        // 1. Handle Offset-based PagedResult<T>
        if(valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(PagedResult<>)) {
            dynamic pagedResult = value;
            PageMetadata metadata = pagedResult.Metadata;

            ApplyOffsetHeaders(httpContext, metadata);
        }
        // 2. Handle Keyset-based CursorResult<T>
        else if(valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(CursorResult<>)) {
            dynamic cursorResult = value;
            CursorMetadata metadata = cursorResult.Metadata;

            ApplyCursorHeaders(httpContext, metadata);
        }

        // 3. Handle ETag & 304 Not Modified
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object? GetValueFromResult(object result) {
        if(result is IValueHttpResult valueResult) {
            return valueResult.Value;
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
        if(query.Count == 0 || (query.Count == 1 && query.ContainsKey("pageNumber"))) {
            return $"{path}?pageNumber={pageNumber}";
        }

        StringBuilder sb = new(path.Value?.Length + 32 ?? 32);
        sb.Append(path.Value);
        char separator = '?';

        foreach(KeyValuePair<string, StringValues> pair in query) {
            if(string.Equals(pair.Key, "pageNumber", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append(separator).Append(pair.Key).Append('=').Append(pair.Value);
            separator = '&';
        }

        sb.Append(separator).Append("pageNumber=").Append(pageNumber);
        return sb.ToString();
    }

    private static string BuildCursorUri(PathString path, IQueryCollection query, CursorToken cursor, CursorDirection direction) {
        StringBuilder sb = new(path.Value?.Length + 64 ?? 64);
        sb.Append(path.Value);
        char separator = '?';

        foreach(KeyValuePair<string, StringValues> pair in query) {
            if(string.Equals(pair.Key, "cursor", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(pair.Key, "direction", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append(separator).Append(pair.Key).Append('=').Append(pair.Value);
            separator = '&';
        }

        sb.Append(separator).Append("cursor=").Append(cursor.Value)
          .Append("&direction=").Append(direction);

        return sb.ToString();
    }
}