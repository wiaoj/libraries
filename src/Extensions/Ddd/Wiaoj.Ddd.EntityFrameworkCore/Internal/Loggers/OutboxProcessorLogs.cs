using Microsoft.Extensions.Logging;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Loggers;
internal static partial class OutboxProcessorLogs {

    // --- Startup & Lifecycle ---
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Outbox Processor service started. Configuration: BatchSize={BatchSize}, PollingInterval={PollingInterval}")]
    public static partial void LogServiceStarted(this ILogger logger, int batchSize, TimeSpan pollingInterval);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Fast-Path (Channel) listener initialized and waiting for messages.")]
    public static partial void LogFastPathStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Fast-Path listener stopped (Cancellation requested).")]
    public static partial void LogFastPathStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Critical,
        Message = "Fast-Path listener encountered a critical error and stopped.")]
    public static partial void LogFastPathCriticalError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Slow-Path (Polling) listener initialized.")]
    public static partial void LogSlowPathStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Error,
        Message = "An unexpected error occurred during the DB polling cycle.")]
    public static partial void LogPollingError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Successfully claimed ownership of {Count} stalled/zombie messages via Polling.")]
    public static partial void LogZombieMessagesClaimed(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Outbox Processor is waiting for {Delay} before starting operations (InitialDelay configured).")]
    public static partial void LogInitialDelayPending(this ILogger logger, TimeSpan delay);
    // --- Message Processing Workflow ---

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Starting processing workflow for message.")]
    public static partial void LogProcessingStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Deserialization failed. Type could not be resolved or content is invalid. Marking message as failed.")]
    public static partial void LogDeserializationFailed(this ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Dispatching domain event handlers...")]
    public static partial void LogDispatchingHandlers(this ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "Domain event handlers executed successfully. Duration: {Duration}ms")]
    public static partial void LogHandlersExecuted(this ILogger logger, long duration);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Message processed and status updated in database successfully. Total Time: {Duration}ms")]
    public static partial void LogMessageProcessedSuccessfully(this ILogger logger, long duration);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Error,
        Message = "An exception occurred while processing message. Duration before failure: {Duration}ms")]
    public static partial void LogProcessingFailed(this ILogger logger, Exception ex, long duration);

    // --- Diagnostics (Update Failures) ---

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Post-processing update failed because the message record no longer exists in the database. It may have been deleted manually.")]
    public static partial void LogDiagRecordNotFound(this ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Post-processing update skipped. The message was already marked as processed by another consumer at {ProcessedAt}. (Idempotency check)")]
    public static partial void LogDiagAlreadyProcessed(this ILogger logger, DateTimeOffset? processedAt);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Post-processing update failed due to Lock Loss. The lock is currently held by instance '{CurrentLockOwner}'. Processing took {Duration}ms.")]
    public static partial void LogDiagLockLost(this ILogger logger, string? currentLockOwner, long duration);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "Post-processing update returned 0 rows affected, but diagnostic check shows valid state. This may indicate a transaction visibility issue or concurrency race condition. Current State: [LockId={LockId}, Expiration={Expiration}]")]
    public static partial void LogDiagRaceCondition(this ILogger logger, string? lockId, DateTimeOffset? expiration);

    // --- Failure Marking ---

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Message marked as failed in database. Retry count incremented.")]
    public static partial void LogMarkedAsFailed(this ILogger logger);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Failed to update error status in database. Lock may have been lost.")]
    public static partial void LogMarkFailedStatusUpdateLost(this ILogger logger);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Error,
        Message = "Critical error while attempting to mark message as failed.")]
    public static partial void LogCriticalMarkFailedError(this ILogger logger, Exception ex);

    // --- Serialization / Types ---

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Error resolving type '{Type}' from assembly.")]
    public static partial void LogTypeResolutionError(this ILogger logger, Exception ex, string type);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message = "Type '{Type}' could not be resolved (Type.GetType returned null).")]
    public static partial void LogTypeResolutionNull(this ILogger logger, string type);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Error,
        Message = "Serialization error while deserializing content to type '{Type}'.")]
    public static partial void LogDeserializationError(this ILogger logger, Exception ex, string type);
}