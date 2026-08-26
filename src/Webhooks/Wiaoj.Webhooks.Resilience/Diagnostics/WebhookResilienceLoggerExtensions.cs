using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.Resilience.Diagnostics;

/// <summary>
/// Structured compile-time zero-allocation logging extension methods for webhook circuit breaker middleware.
/// </summary>
internal static partial class WebhookResilienceLoggerExtensions {
    [LoggerMessage(
        EventId = 4401,
        Level = LogLevel.Warning,
        Message = "Circuit breaker is OPEN for webhook endpoint '{EndpointId}'. Fast-failing delivery and re-enqueuing with delay {RetryAfterMs:F0}ms.")]
    public static partial void LogCircuitBreakerOpenFastFailed(
        this ILogger logger,
        string endpointId,
        double retryAfterMs);
}