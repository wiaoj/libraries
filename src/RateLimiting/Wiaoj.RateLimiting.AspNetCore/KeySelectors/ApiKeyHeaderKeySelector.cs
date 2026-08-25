using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Extracts a rate limiting key based on an HTTP header (e.g. <c>X-Api-Key</c>).
/// </summary>
public sealed class ApiKeyHeaderKeySelector : IRateLimitKeySelector<HttpContext> {
    private readonly string _headerName;
    private readonly string _prefix;
    private readonly IRateLimitKeySelector<HttpContext> _fallbackSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyHeaderKeySelector"/> class with default header and fallback selector.
    /// </summary>
    public ApiKeyHeaderKeySelector()
        : this(RateLimitConstants.Headers.DefaultApiKey, "apikey:", new ClientIpKeySelector("anonymous_ip:")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyHeaderKeySelector"/> class with a specified header name.
    /// </summary>
    /// <param name="headerName">The name of the HTTP header containing the API key.</param>
    public ApiKeyHeaderKeySelector(string headerName)
        : this(headerName, "apikey:", new ClientIpKeySelector("anonymous_ip:")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyHeaderKeySelector"/> class with a specified header name and key prefix.
    /// </summary>
    /// <param name="headerName">The name of the HTTP header containing the API key.</param>
    /// <param name="prefix">The key prefix for scope isolation.</param>
    public ApiKeyHeaderKeySelector(string headerName, string prefix)
        : this(headerName, prefix, new ClientIpKeySelector("anonymous_ip:")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyHeaderKeySelector"/> class with header name, key prefix, and fallback selector.
    /// </summary>
    /// <param name="headerName">The name of the HTTP header containing the API key.</param>
    /// <param name="prefix">The key prefix for scope isolation.</param>
    /// <param name="fallbackSelector">The fallback selector invoked when the specified header is missing or empty.</param>
    public ApiKeyHeaderKeySelector(
        string headerName,
        string prefix,
        IRateLimitKeySelector<HttpContext> fallbackSelector) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        Preca.ThrowIfNull(prefix);
        Preca.ThrowIfNull(fallbackSelector);

        this._headerName = headerName;
        this._prefix = prefix;
        this._fallbackSelector = fallbackSelector;
    }

    /// <inheritdoc/>
    public string GetKey(HttpContext context) {
        Preca.ThrowIfNull(context);

        if(context.Request.Headers.TryGetValue(this._headerName, out StringValues values) && values.Count > 0) {
            string? key = values[0];
            if(!string.IsNullOrWhiteSpace(key)) {
                return string.Concat(this._prefix, key);
            }
        }

        return this._fallbackSelector.GetKey(context);
    }
}