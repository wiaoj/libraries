using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.AspNetCore.Diagnostics;

/// <summary>
/// Structured zero-allocation logging extensions for the inbound webhook receiver engine.
/// </summary>
public static partial class WebhookReceiverLoggerExtensions {
    /// <summary>Logs that an incoming webhook request was received.</summary>
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Debug,
        Message = "Received inbound webhook request for event '{EventName}' on path '{Path}'.")]
    public static partial void LogInboundWebhookReceived(this ILogger logger, string eventName, string path);

    /// <summary>Logs that an inbound webhook was rejected due to signature verification failure.</summary>
    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Inbound webhook signature verification failed on path '{Path}'. Request rejected with 401 Unauthorized.")]
    public static partial void LogInboundSignatureVerificationFailed(this ILogger logger, string path);

    /// <summary>Logs that an incoming webhook was detected as duplicate and safely suppressed.</summary>
    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Debug,
        Message = "Duplicate inbound webhook intercepted with key '{IdempotencyKey}'. Skipping execution and returning 200 OK.")]
    public static partial void LogInboundDuplicateSkipped(this ILogger logger, string idempotencyKey);

    /// <summary>Logs that an incoming webhook was successfully processed.</summary>
    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Debug,
        Message = "Inbound webhook event '{EventName}' successfully processed in {DurationMs:F2}ms.")]
    public static partial void LogInboundWebhookProcessed(this ILogger logger, string eventName, double durationMs);
}