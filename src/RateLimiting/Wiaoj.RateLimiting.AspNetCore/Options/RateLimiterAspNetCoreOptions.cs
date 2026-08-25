using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Configuration options for ASP.NET Core rate limiting middleware.
/// </summary>
public sealed class RateLimiterAspNetCoreOptions {
    /// <summary>
    /// Gets or sets the default key selector used to extract rate limiting keys from requests.
    /// </summary>
    public IRateLimitKeySelector<HttpContext> KeySelector { get; set; } = new ClientIpKeySelector();

    /// <summary>
    /// Gets or sets the default fallback cost resolver when no endpoint metadata is specified.
    /// </summary>
    public Func<HttpContext, int> DefaultCostResolver { get; set; } = static _ => 1;

    /// <summary>
    /// Gets or sets the HTTP status code returned when a request is rate-limited. Defaults to 429.
    /// </summary>
    public int StatusCode { get; set; } = StatusCodes.Status429TooManyRequests;

    /// <summary>
    /// Gets or sets whether to write IETF RateLimit-* draft headers.
    /// </summary>
    public bool EnableIetfHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to return an RFC 7807/9457 ProblemDetails JSON payload on 429 rejections.
    /// </summary>
    public bool UseProblemDetails { get; set; } = true;

    /// <summary>
    /// Custom delegate to enrich or modify the generated <see cref="ProblemDetails"/> before it is serialized.
    /// </summary>
    public Action<ProblemDetails, HttpContext, RateLimitDecision>? ProblemDetailsCustomizer { get; set; }

    /// <summary>
    /// Optional low-level custom rejection delegate. If set, overrides standard ProblemDetails rendering.
    /// </summary>
    public Func<HttpContext, RateLimitDecision, Task>? OnRejectedAsync { get; set; }
}