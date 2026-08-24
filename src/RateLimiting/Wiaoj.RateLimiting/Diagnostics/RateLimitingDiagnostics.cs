using Microsoft.Extensions.Logging;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Unified diagnostics facade orchestrating OpenTelemetry metrics and structured logging.
/// </summary>
public static class RateLimitingDiagnostics {
    /// <summary>
    /// Records both metrics and logs for an evaluated rate-limit decision.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <param name="key">The rate limiting key.</param>
    /// <param name="cost">The operation cost.</param>
    /// <param name="decision">The outcome of the rate limiting check.</param>
    public static void RecordDecision(
        ILogger logger,
        string algorithm,
        string key,
        int cost,
        RateLimitDecision decision) {

        RateLimitingMetrics.RecordDecision(algorithm, decision.IsAllowed, cost);

        if(decision.IsAllowed) {
            logger.LogAcquireAllowed(key, algorithm, cost, decision.Remaining);

        }
        else {
            double? retryAfterSec = decision.RetryAfter?.TotalSeconds;
            logger.LogAcquireDenied(key, algorithm, cost, retryAfterSec, decision.Remaining);
        }
    }

    /// <summary>
    /// Records both metrics and logs for a queued request delay.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <param name="key">The rate limiting key.</param>
    /// <param name="cost">The operation cost.</param>
    /// <param name="waitDuration">The time span the request is scheduled to wait.</param>
    public static void RecordQueueSuspended(
        ILogger logger,
        string algorithm,
        string key,
        int cost,
        TimeSpan waitDuration) {

        double ms = waitDuration.TotalMilliseconds;
        RateLimitingMetrics.RecordQueueWait(algorithm, ms);
        logger.LogQueueSuspended(key, algorithm, ms, cost);
    }

    /// <summary>
    /// Records logs when a queued request completes waiting.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <param name="key">The rate limiting key.</param>
    /// <param name="elapsedWait">The actual time span elapsed during wait.</param>
    public static void RecordQueueReleased(
        ILogger logger,
        string algorithm,
        string key,
        TimeSpan elapsedWait) {
        logger.LogQueueReleased(key, algorithm, elapsedWait.TotalMilliseconds);
    }

    /// <summary>
    /// Records a trace log for speculative rollback.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <param name="key">The rate limiting key.</param>
    /// <param name="cost">The rolled-back cost.</param>
    /// <param name="reason">The reason for the rollback.</param>
    public static void RecordRollback(
        ILogger logger,
        string algorithm,
        string key,
        int cost,
        string reason) {
        logger.LogRollbackExecuted(key, algorithm, cost, reason);
    }

    /// <summary>
    /// Records a warning log when a queued request is cancelled.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <param name="key">The rate limiting key.</param>
    /// <param name="cost">The cancelled cost.</param>
    public static void RecordQueueCancelled(
        ILogger logger,
        string algorithm,
        string key,
        int cost) {
        logger.LogQueueCancelled(key, algorithm, cost);
    }
}