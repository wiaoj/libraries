using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.DistributedCounter.Diagnostics;

/// <summary>
/// Structured logging extension methods for the Distributed Counter Webhook rate limiting middleware.
/// Uses [LoggerMessage] source generator.
/// </summary>
public static partial class DistributedRateLimitingLoggerExtensions {

    /// <summary>Logs that a rate limit was exceeded and the webhook delivery was re-enqueued with a delay.</summary>
    [LoggerMessage(
        EventId = 4301,
        Level = LogLevel.Warning,
        Message = "Rate limit of {MaxRequests} requests per {WindowMs:F0}ms exceeded for endpoint '{EndpointId}'. Re-enqueuing delivery.")]
    public static partial void LogRateLimitExceeded(this ILogger logger, long maxRequests, double windowMs, string endpointId);
}
