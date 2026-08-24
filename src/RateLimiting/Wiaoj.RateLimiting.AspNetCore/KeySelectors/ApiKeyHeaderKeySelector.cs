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
    /// Initializes a new instance of the <see cref="ApiKeyHeaderKeySelector"/> class.
    /// </summary>
    /// <param name="headerName">The name of the HTTP header containing the API key. Defaults to <see cref="RateLimitConstants.Headers.DefaultApiKey"/>.</param>
    /// <param name="prefix">An optional key prefix for scope isolation (e.g. <c>"apikey:"</c>). Defaults to <c>"apikey:"</c>.</param>
    /// <param name="fallbackSelector">
    /// An optional fallback selector invoked when the specified header is missing or empty.
    /// Defaults to a <see cref="ClientIpKeySelector"/> prefixed with <c>"anonymous_ip:"</c>.
    /// </param>
    public ApiKeyHeaderKeySelector(
        string headerName = RateLimitConstants.Headers.DefaultApiKey,
        string prefix = "apikey:",
        IRateLimitKeySelector<HttpContext>? fallbackSelector = null) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this._headerName = headerName;
        this._prefix = prefix ?? string.Empty;
        this._fallbackSelector = fallbackSelector ?? new ClientIpKeySelector("anonymous_ip:");
    }

    /// <inheritdoc />
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