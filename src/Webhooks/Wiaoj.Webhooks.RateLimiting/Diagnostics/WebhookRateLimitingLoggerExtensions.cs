using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.RateLimiting.Diagnostics;

/// <summary>
/// Structured compile-time zero-allocation logging extension methods for outbound webhook rate limiting middleware.
/// </summary>
internal static partial class WebhookRateLimitingLoggerExtensions {
    [LoggerMessage(
        EventId = 4301,
        Level = LogLevel.Warning,
        Message = "Rate limit exceeded for webhook endpoint '{EndpointId}'. Re-enqueuing delivery with delay {RetryAfterMs:F0}ms.")]
    public static partial void LogRateLimitExceeded(
        this ILogger logger,
        string endpointId,
        double retryAfterMs);
}