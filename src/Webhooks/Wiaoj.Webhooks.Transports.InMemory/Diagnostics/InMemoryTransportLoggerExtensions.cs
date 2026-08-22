using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.Transports.InMemory.Diagnostics;

/// <summary>
/// High-performance structured logging extensions for <see cref="InMemoryWebhookConsumer"/>, <see cref="InMemoryWebhookTransport"/>,
/// and <see cref="Internal.InMemoryDelayedScheduler"/>.
/// Uses [LoggerMessage] source generator.
/// </summary>
public static partial class InMemoryTransportLoggerExtensions {

    // ── Trace (1100 - 1199) ───────────────────────────────────────────────────

    /// <summary>Logs that a job is being enqueued directly to the in-memory channel.</summary>
    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Trace,
        Message = "Enqueueing job '{JobId}' for endpoint '{EndpointId}' into in-memory channel immediately.")]
    public static partial void LogJobEnqueuingImmediate(this ILogger logger, string jobId, string endpointId);

    /// <summary>Logs that a delayed job's timer was cancelled before expiry.</summary>
    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Trace,
        Message = "Cancelled scheduled delayed timer for job '{JobId}' (endpoint: '{EndpointId}').")]
    public static partial void LogDelayedJobCancelled(this ILogger logger, string jobId, string endpointId);

    /// <summary>Logs that a consumer worker dequeued a job from the channel.</summary>
    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Trace,
        Message = "Worker #{WorkerId} dequeued job '{JobId}' for endpoint '{EndpointId}'.")]
    public static partial void LogWorkerDequeuedJob(this ILogger logger, int workerId, string jobId, string endpointId);

    // ── Debug (2100 - 2199) ───────────────────────────────────────────────────

    /// <summary>Logs that a job has been scheduled with a non-blocking delay timer.</summary>
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Debug,
        Message = "Scheduled delayed job '{JobId}' for endpoint '{EndpointId}' (due in {DelayMs:F0}ms).")]
    public static partial void LogJobScheduledDelayed(this ILogger logger, string jobId, string endpointId, double delayMs);

    /// <summary>Logs that a delayed job timer fired and the job was written to the active channel.</summary>
    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Debug,
        Message = "Delay expired for job '{JobId}' (endpoint: '{EndpointId}'). Enqueued to active channel.")]
    public static partial void LogDelayedJobFlushed(this ILogger logger, string jobId, string endpointId);

    // ── Information (3100 - 3199) ─────────────────────────────────────────────

    /// <summary>Logs that the in-memory consumer background pool started with N concurrent workers.</summary>
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "Started in-memory webhook consumer with {Concurrency} concurrent workers.")]
    public static partial void LogConsumerStarted(this ILogger logger, int concurrency);

    /// <summary>Logs that the in-memory consumer background pool is stopping.</summary>
    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Information,
        Message = "Stopping in-memory webhook consumer gracefully.")]
    public static partial void LogConsumerStopping(this ILogger logger);

    // ── Error (5100 - 5199) ───────────────────────────────────────────────────

    /// <summary>Logs an unhandled exception during job processing in a consumer worker loop.</summary>
    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Error,
        Message = "Worker #{WorkerId} encountered unhandled exception while processing job '{JobId}' for endpoint '{EndpointId}'.")]
    public static partial void LogConsumerJobError(this ILogger logger, Exception exception, int workerId, string jobId, string endpointId);

    // ── Critical (6100 - 6199) ────────────────────────────────────────────────

    /// <summary>Logs a fatal crash in the consumer host.</summary>
    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Critical,
        Message = "Fatal crash in in-memory webhook consumer worker loop.")]
    public static partial void LogConsumerFatalError(this ILogger logger, Exception exception);
}
