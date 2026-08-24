#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.RateLimiting.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Centralized constants for rate limiting HTTP headers, URIs, and content types.
/// </summary>
public static class RateLimitConstants {
    /// <summary>
    /// Well-known HTTP header names used by RFC specifications and IETF RateLimit drafts.
    /// </summary>
    public static class Headers {
        /// <summary>RFC 9110: Standard retry-after header in delta-seconds.</summary>
        public const string RetryAfter = "Retry-After";

        /// <summary>IETF Draft: Maximum request quota limit within the current window.</summary>
        public const string RateLimitLimit = "RateLimit-Limit";

        /// <summary>IETF Draft: Number of remaining quota units in the current window.</summary>
        public const string RateLimitRemaining = "RateLimit-Remaining";

        /// <summary>IETF Draft: Number of seconds until the rate limit window resets.</summary>
        public const string RateLimitReset = "RateLimit-Reset";

        /// <summary>IETF Draft: Name or definition of the matching rate limiting policy.</summary>
        public const string RateLimitPolicy = "RateLimit-Policy";

        /// <summary>Default API key header name.</summary>
        public const string DefaultApiKey = "X-Api-Key";
    }

    /// <summary>
    /// Specification and documentation URIs for rate limiting RFCs.
    /// </summary>
    public static class Uris {
        /// <summary>RFC 6585 specification URI for 429 Too Many Requests.</summary>
        public const string Rfc6585 = "https://tools.ietf.org/html/rfc6585#section-4";
    }

    /// <summary>
    /// Standard MIME content types used in rate limiting responses.
    /// </summary>
    public static class ContentTypes {
        /// <summary>RFC 7807/9457 ProblemDetails JSON content type.</summary>
        public const string ProblemJson = "application/problem+json";

        /// <summary>Plain text fallback content type.</summary>
        public const string PlainText = "text/plain";
    }
}