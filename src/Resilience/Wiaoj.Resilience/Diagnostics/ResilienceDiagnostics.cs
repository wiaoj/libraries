using Microsoft.Extensions.Logging;

namespace Wiaoj.Resilience.Diagnostics;

/// <summary>
/// Internal diagnostics facade coordinating metrics and logging for resilience operations.
/// </summary>
internal static class ResilienceDiagnostics {
    public static void RecordDecision(
        ILogger logger,
        string strategy,
        string key,
        CircuitExecutionDecision decision) {

        ResilienceMetrics.RecordDecision(strategy, key, decision.State, decision.IsAllowed);

        if(!decision.IsAllowed) {
            double retrySec = decision.RetryAfter?.TotalSeconds ?? 0;
            logger.LogExecutionDenied(strategy, key, retrySec);
        }
        else if(decision.State == CircuitState.HalfOpen) {
            logger.LogProbePermitted(strategy, key);
        }
    }

    public static void RecordTrip(
        ILogger logger,
        string strategy,
        string key,
        string reason,
        TimeSpan breakDuration) {

        ResilienceMetrics.RecordTrip(strategy, key);
        logger.LogCircuitTripped(strategy, key, reason, breakDuration.TotalMilliseconds);
    }

    public static void RecordSuccess(
        ILogger logger,
        string strategy,
        string key,
        bool wasRecovered = false) {

        ResilienceMetrics.RecordSuccess(strategy, key, wasRecovered);

        if(wasRecovered) {
            logger.LogCircuitClosed(strategy, key);
        }
    }

    public static void RecordFailure(
        ILogger logger,
        string strategy,
        string key,
        double metric) {

        ResilienceMetrics.RecordFailure(strategy, key);
        logger.LogTransientFailure(strategy, key, metric);
    }
}