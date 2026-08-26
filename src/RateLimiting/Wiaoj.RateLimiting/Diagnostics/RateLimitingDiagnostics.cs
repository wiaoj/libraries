using Microsoft.Extensions.Logging;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Internal diagnostics facade coordinating metrics and logging for rate limiting.
/// </summary>
internal static class RateLimitingDiagnostics {
    public static void RecordDecision(
        ILogger logger,
        string algorithm,
        string key,
        int cost,
        RateLimitDecision decision,
        string policy = "Default") {

        RateLimitingMetrics.RecordDecision(policy, algorithm, decision.IsAllowed, cost);

        if(decision.IsAllowed) {
            logger.LogAcquireAllowed(key, algorithm, cost, decision.Remaining);
        }
        else {
            double? retryAfterSec = decision.RetryAfter?.TotalSeconds;
            logger.LogAcquireDenied(key, algorithm, cost, retryAfterSec, decision.Remaining);
        }
    }

    public static void RecordQueueSuspended(
        ILogger logger,
        string algorithm,
        string key,
        int cost,
        TimeSpan waitDuration,
        string policy = "Default") {

        double ms = waitDuration.TotalMilliseconds;
        RateLimitingMetrics.RecordQueueWait(policy, algorithm, ms);
        logger.LogQueueSuspended(key, algorithm, ms, cost);
    }

    public static void RecordQueueReleased(
        ILogger logger,
        string algorithm,
        string key,
        TimeSpan elapsedWait) {
        logger.LogQueueReleased(key, algorithm, elapsedWait.TotalMilliseconds);
    }

    public static void RecordRollback(
        ILogger logger,
        string algorithm,
        string key,
        int cost,
        string reason) {
        logger.LogRollbackExecuted(key, algorithm, cost, reason);
    }

    public static void RecordQueueCancelled(
        ILogger logger,
        string algorithm,
        string key,
        int cost) {
        logger.LogQueueCancelled(key, algorithm, cost);
    }
}