using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.Diagnostics;

/// <summary>
/// High-performance, zero-allocation structured logging extension methods for the Wiaoj Webhooks engine.
/// Uses the C# [LoggerMessage] source generator with strongly-typed domain value objects.
/// </summary>
internal static partial class WebhookLoggerExtensions {

    // ── Trace (1000 - 1999) ───────────────────────────────────────────────────

    /// <summary>Logs that dispatching a webhook event is starting.</summary>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Trace,
        Message = "Starting dispatch for webhook event '{EventName}' to endpoint '{EndpointId}'.")]
    public static partial void LogDispatchStarting(this ILogger logger, string eventName, WebhookEndpointId endpointId);

    /// <summary>Logs that a delivery attempt is starting through the execution pipeline.</summary>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Trace,
        Message = "Starting delivery attempt #{AttemptNumber} for endpoint '{EndpointId}' (target: '{TargetUrl}').")]
    public static partial void LogDeliveryAttemptStarting(this ILogger logger, int attemptNumber, WebhookEndpointId endpointId, Uri targetUrl);

    /// <summary>Logs that an HTTP request is being dispatched to the target endpoint.</summary>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Trace,
        Message = "Sending HTTP POST request to '{TargetUrl}' with {PayloadLength} bytes.")]
    public static partial void LogHttpRequestIssuing(this ILogger logger, Uri targetUrl, int payloadLength);

    /// <summary>Logs that a job is being persisted to the store.</summary>
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Trace,
        Message = "Persisting job record '{JobId}' for endpoint '{EndpointId}' (event: '{EventName}').")]
    public static partial void LogStoreSavingJob(this ILogger logger, WebhookJobId jobId, WebhookEndpointId endpointId, string eventName);

    /// <summary>Logs that a batch dispatch operation is starting.</summary>
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Trace,
        Message = "Starting batch dispatch '{BatchId}' with {EventCount} events to endpoint '{EndpointId}'.")]
    public static partial void LogBatchDispatchStarting(this ILogger logger, string batchId, int eventCount, WebhookEndpointId endpointId);

    // ── Debug (2000 - 2999) ───────────────────────────────────────────────────

    /// <summary>Logs that a job was successfully persisted in the store.</summary>
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Job '{JobId}' saved to store with initial status '{Status}'.")]
    public static partial void LogStoreSavedJob(this ILogger logger, WebhookJobId jobId, WebhookJobStatus status);

    /// <summary>Logs that an endpoint was resolved successfully.</summary>
    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Resolved endpoint '{EndpointId}' to target URL '{TargetUrl}'.")]
    public static partial void LogEndpointResolved(this ILogger logger, WebhookEndpointId endpointId, Uri targetUrl);

    /// <summary>Logs that an HTTP response was received from the target endpoint.</summary>
    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Received HTTP {StatusCode} from '{TargetUrl}' in {DurationMs:F2}ms.")]
    public static partial void LogHttpResponseReceived(this ILogger logger, int statusCode, Uri targetUrl, double durationMs);

    /// <summary>Logs that a webhook payload was successfully signed with cryptographic signature.</summary>
    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "Payload for endpoint '{EndpointId}' signed with '{Algorithm}' (timestamp: {Timestamp}).")]
    public static partial void LogSigningCompleted(this ILogger logger, WebhookEndpointId endpointId, string algorithm, long timestamp);

    /// <summary>Logs that a job status was updated in the persistent store.</summary>
    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Debug,
        Message = "Job '{JobId}' status transitioned to '{NewStatus}'.")]
    public static partial void LogStoreStatusUpdated(this ILogger logger, WebhookJobId jobId, WebhookJobStatus newStatus);

    /// <summary>Logs that a delivery attempt was recorded in the persistent audit trail.</summary>
    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Debug,
        Message = "Recorded attempt #{AttemptNumber} for job '{JobId}' (success: {IsSuccess}, duration: {DurationMs:F2}ms).")]
    public static partial void LogStoreAttemptRecorded(this ILogger logger, WebhookJobId jobId, int attemptNumber, bool isSuccess, double durationMs);

    /// <summary>Logs that an instance successfully claimed a lease lock on a job.</summary>
    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Debug,
        Message = "Instance '{InstanceId}' claimed lease lock for job '{JobId}' for {LeaseDurationMs}ms.")]
    public static partial void LogStoreLeaseClaimed(this ILogger logger, string instanceId, WebhookJobId jobId, double leaseDurationMs);

    /// <summary>Logs that a background stale job recovery sweep is starting.</summary>
    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Debug,
        Message = "Starting stale in-flight job recovery sweep with threshold '{Threshold}'.")]
    public static partial void LogRecoverySweepStarting(this ILogger logger, DateTimeOffset threshold);

    // ── Information (3000 - 3999) ─────────────────────────────────────────────

    /// <summary>Logs that a webhook event has been dispatched.</summary>
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Webhook event '{EventName}' dispatched as job '{JobId}' to endpoint '{EndpointId}'.")]
    public static partial void LogDispatchCompleted(this ILogger logger, string eventName, WebhookJobId jobId, WebhookEndpointId endpointId);

    /// <summary>Logs that a webhook delivery attempt succeeded with job context.</summary>
    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Webhook delivery attempt #{AttemptNumber} for job '{JobId}' to endpoint '{EndpointId}' succeeded with HTTP {StatusCode} in {DurationMs:F2}ms.")]
    public static partial void LogDeliverySuccess(this ILogger logger, WebhookJobId jobId, int attemptNumber, WebhookEndpointId endpointId, int? statusCode, double durationMs);

    /// <summary>Logs that a webhook delivery attempt succeeded without job context.</summary>
    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Webhook delivery attempt #{AttemptNumber} to endpoint '{EndpointId}' succeeded with HTTP {StatusCode} in {DurationMs:F2}ms.")]
    public static partial void LogDeliverySuccess(this ILogger logger, int attemptNumber, WebhookEndpointId endpointId, int? statusCode, double durationMs);

    /// <summary>Logs that stale jobs were successfully recovered and re-enqueued.</summary>
    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Successfully recovered {RecoveredCount} stale in-flight webhook jobs and re-enqueued them for delivery.")]
    public static partial void LogRecoverySweepCompleted(this ILogger logger, int recoveredCount);

    /// <summary>Logs that a batch dispatch operation completed successfully.</summary>
    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Information,
        Message = "Successfully dispatched batch '{BatchId}' with {EventCount} events to endpoint '{EndpointId}'.")]
    public static partial void LogBatchDispatchCompleted(this ILogger logger, string batchId, int eventCount, WebhookEndpointId endpointId);

    // ── Warning (4000 - 4999) ─────────────────────────────────────────────────

    /// <summary>Logs that a webhook delivery attempt failed with a non-success outcome with job context.</summary>
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Webhook delivery attempt #{AttemptNumber} for job '{JobId}' to endpoint '{EndpointId}' failed (HTTP {StatusCode}, {DurationMs:F2}ms): {ErrorMessage}")]
    public static partial void LogDeliveryAttemptWarning(this ILogger logger, WebhookJobId jobId, int attemptNumber, WebhookEndpointId endpointId, int? statusCode, string? errorMessage, double durationMs);

    /// <summary>Logs that a webhook delivery attempt failed with a non-success outcome without job context.</summary>
    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Webhook delivery attempt #{AttemptNumber} to endpoint '{EndpointId}' failed (HTTP {StatusCode}, {DurationMs:F2}ms): {ErrorMessage}")]
    public static partial void LogDeliveryAttemptWarning(this ILogger logger, int attemptNumber, WebhookEndpointId endpointId, int? statusCode, string? errorMessage, double durationMs);

    /// <summary>Logs that the delivery pipeline short-circuited.</summary>
    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Warning,
        Message = "Webhook pipeline for endpoint '{EndpointId}' short-circuited: {Reason}")]
    public static partial void LogPipelineShortCircuited(this ILogger logger, WebhookEndpointId endpointId, string reason);

    /// <summary>Logs that an HTTP delivery request timed out.</summary>
    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "HTTP request to '{TargetUrl}' for endpoint '{EndpointId}' timed out.")]
    public static partial void LogHttpRequestTimedOut(this ILogger logger, Uri targetUrl, WebhookEndpointId endpointId);

    /// <summary>Logs that a failed webhook delivery attempt has been scheduled for a retry.</summary>
    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Warning,
        Message = "Webhook delivery attempt #{AttemptNumber} for endpoint '{EndpointId}' failed. Next retry scheduled in {DelayMs:F0}ms.")]
    public static partial void LogRetryScheduled(this ILogger logger, int attemptNumber, WebhookEndpointId endpointId, double delayMs);

    /// <summary>Logs lease lock contention when another instance already holds an active lease on a job.</summary>
    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Warning,
        Message = "Instance '{InstanceId}' failed to claim lease for job '{JobId}'. Active lease held by '{LockedBy}' until {LockExpiresAt}.")]
    public static partial void LogStoreLeaseContention(this ILogger logger, string instanceId, WebhookJobId jobId, string? lockedBy, DateTimeOffset? lockExpiresAt);

    /// <summary>Logs high lock contention when waiting to acquire endpoint delivery lock.</summary>
    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Warning,
        Message = "High lock contention detected for endpoint '{EndpointId}'. Waited {LockWaitDurationMs:F2}ms to acquire delivery lock.")]
    public static partial void LogLockContention(this ILogger logger, WebhookEndpointId endpointId, double lockWaitDurationMs);

    // ── Error (5000 - 5999) ───────────────────────────────────────────────────

    /// <summary>Logs that an unexpected error occurred during webhook job handling.</summary>
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Unhandled exception during job processing for endpoint '{EndpointId}'.")]
    public static partial void LogJobProcessingError(this ILogger logger, Exception exception, WebhookEndpointId endpointId);

    /// <summary>Logs that endpoint resolution failed without job context.</summary>
    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Failed to resolve endpoint '{EndpointId}'.")]
    public static partial void LogEndpointResolutionFailed(this ILogger logger, Exception? exception, WebhookEndpointId endpointId);

    /// <summary>Logs that endpoint resolution failed for a specific job.</summary>
    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Error,
        Message = "Failed to resolve endpoint '{EndpointId}' for job '{JobId}'.")]
    public static partial void LogEndpointResolutionFailed(this ILogger logger, Exception? exception, WebhookJobId jobId, WebhookEndpointId endpointId);

    /// <summary>Logs that a webhook delivery exhausted all retry attempts and failed permanently (dead letter).</summary>
    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Error,
        Message = "Webhook delivery for endpoint '{EndpointId}' permanently failed after {TotalAttempts} attempts and has been moved to dead letter.")]
    public static partial void LogDeliveryPermanentlyFailed(this ILogger logger, WebhookEndpointId endpointId, int totalAttempts);

    /// <summary>Logs that dispatching a webhook failed.</summary>
    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Error,
        Message = "Failed to dispatch webhook event '{EventName}' to endpoint '{EndpointId}'.")]
    public static partial void LogDispatchFailed(this ILogger logger, Exception exception, string eventName, WebhookEndpointId endpointId);

    /// <summary>Logs that an error occurred during the background recovery sweep.</summary>
    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Error,
        Message = "Unexpected error occurred during background stale job recovery sweep.")]
    public static partial void LogRecoverySweepFailed(this ILogger logger, Exception exception);

    /// <summary>Logs that a batch dispatch operation failed.</summary>
    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Error,
        Message = "Failed to dispatch batch '{BatchId}' with {EventCount} events to endpoint '{EndpointId}'.")]
    public static partial void LogBatchDispatchFailed(this ILogger logger, Exception exception, string batchId, int eventCount, WebhookEndpointId endpointId);

    // ── Critical (6000 - 6999) ────────────────────────────────────────────────

    /// <summary>Logs a critical failure in the webhook engine or store crash.</summary>
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Critical,
        Message = "Fatal failure in webhook engine for endpoint '{EndpointId}'.")]
    public static partial void LogCriticalEngineFailure(this ILogger logger, Exception exception, WebhookEndpointId endpointId);
}