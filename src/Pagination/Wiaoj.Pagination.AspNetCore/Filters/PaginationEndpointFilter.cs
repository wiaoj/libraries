using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Text.Json;
using Wiaoj.Pagination.AspNetCore.Caching;
using Wiaoj.Pagination.AspNetCore.Linking;
using Wiaoj.Preconditions;

namespace Wiaoj.Pagination.AspNetCore.Filters;

/// <summary>
/// An endpoint filter that automatically appends RFC 8288 <c>Link</c> headers, <c>Pagination</c> metadata, 
/// <c>ETag</c> headers, and evaluates conditional <c>304 Not Modified</c> requests for paginated results.
/// </summary>
public sealed class PaginationEndpointFilter : IEndpointFilter {

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
            byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            string etag = ETagGenerator.GenerateWeakETag(utf8Bytes);

            httpContext.Response.Headers[HeaderNames.ETag] = etag;

            string? ifNoneMatch = httpContext.Request.Headers[HeaderNames.IfNoneMatch];
            if(ETagGenerator.IsNotModified(ifNoneMatch, etag)) {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
        }

        return result;
    }

    private void ApplyOffsetHeaders(HttpContext httpContext, PageMetadata metadata) {
        if(metadata.IsEmpty) return;

        // Raw Metadata Header (e.g. X-Pagination)
        if(!string.IsNullOrEmpty(this._options.MetadataHeaderName)) {
            httpContext.Response.Headers[this._options.MetadataHeaderName] = metadata.ToString();
        }

        // RFC 8288 Link Header
        if(this._options.EnableLinkHeaders) {
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, page => {
                QueryString queryParams = QueryString.Create(httpContext.Request.Query);
                queryParams = queryParams.Add("pageNumber", page.ToString());
                return $"{httpContext.Request.Path}{queryParams}";
            });

            if(!string.IsNullOrEmpty(linkHeader)) {
                httpContext.Response.Headers[HeaderNames.Link] = linkHeader;
            }
        }
    }

    private void ApplyCursorHeaders(HttpContext httpContext, CursorMetadata metadata) {
        if(metadata.IsEmpty) return;

        // RFC 8288 Link Header
        if(this._options.EnableLinkHeaders) {
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, (cursor, direction) => {
                QueryString queryParams = QueryString.Create(httpContext.Request.Query);
                queryParams = queryParams.Add("cursor", cursor.Value);
                queryParams = queryParams.Add("direction", direction.ToString());
                return $"{httpContext.Request.Path}{queryParams}";
            });

            if(!string.IsNullOrEmpty(linkHeader)) {
                httpContext.Response.Headers[HeaderNames.Link] = linkHeader;
            }
        }
    }

    private static object? GetValueFromResult(object result) {
        if(result is IValueHttpResult valueResult) {
            return valueResult.Value;
        }
        return result;
    }
}