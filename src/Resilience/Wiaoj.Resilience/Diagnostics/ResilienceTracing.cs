using System.Diagnostics;
using System.Reflection;

namespace Wiaoj.Resilience.Diagnostics;

/// <summary>
/// Distributed tracing ActivitySource provider for circuit breaker and timeout operations.
/// </summary>
internal static class ResilienceTracing {
    public const string SourceName = "Wiaoj.Resilience";

    public const string ExecuteActivityName = "circuit_breaker.execute";
    public const string TimeoutActivityName = "timeout.execute";

    private static readonly string SourceVersion =
        typeof(ResilienceTracing).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ResilienceTracing).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    public static readonly ActivitySource Source = new(SourceName, SourceVersion);

    /// <summary>Tag names applied to resilience activities.</summary>
    public static class Tags {
        public const string Key = "resilience.key";
        public const string CircuitState = "resilience.circuit_state";
        public const string Probe = "resilience.probe";
        public const string Outcome = "resilience.outcome";
        public const string RetryAfterMs = "resilience.retry_after_ms";
        public const string TimeoutMs = "resilience.timeout_ms";
    }

    /// <summary>Values reported through the <see cref="Tags.Outcome"/> tag.</summary>
    public static class Outcomes {
        public const string Success = "success";
        public const string Failure = "failure";
        public const string Denied = "denied";
        public const string Timeout = "timeout";
        public const string Cancelled = "cancelled";
    }

    /// <summary>
    /// Starts a circuit breaker execution activity. Returns <see langword="null"/> when no listener is subscribed,
    /// in which case every operation below is a no-op.
    /// </summary>
    public static Activity? StartExecution(string key) {
        Activity? activity = Source.StartActivity(ExecuteActivityName, ActivityKind.Internal);
        activity?.SetTag(Tags.Key, key);
        return activity;
    }

    /// <summary>Starts a timeout execution activity carrying the configured deadline.</summary>
    public static Activity? StartTimeout(string key, TimeSpan timeout) {
        Activity? activity = Source.StartActivity(TimeoutActivityName, ActivityKind.Internal);

        if(activity is null) {
            return null;
        }

        activity.SetTag(Tags.Key, key);
        activity.SetTag(Tags.TimeoutMs, (long)timeout.TotalMilliseconds);
        return activity;
    }

    /// <summary>Records the acquire decision on the activity before the operation runs.</summary>
    public static void RecordDecision(Activity? activity, CircuitExecutionDecision decision) {
        if(activity is null) {
            return;
        }

        activity.SetTag(Tags.CircuitState, decision.State.ToString());

        if(decision.State == CircuitState.HalfOpen) {
            activity.SetTag(Tags.Probe, true);
        }
    }

    /// <summary>Marks the activity as a completed, successful execution.</summary>
    public static void MarkSuccess(Activity? activity) {
        if(activity is null) {
            return;
        }

        activity.SetTag(Tags.Outcome, Outcomes.Success);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>Marks the activity as rejected by an open circuit.</summary>
    public static void MarkDenied(Activity? activity, TimeSpan? retryAfter) {
        if(activity is null) {
            return;
        }

        activity.SetTag(Tags.Outcome, Outcomes.Denied);

        if(retryAfter.HasValue) {
            activity.SetTag(Tags.RetryAfterMs, (long)retryAfter.Value.TotalMilliseconds);
        }

        activity.SetStatus(ActivityStatusCode.Error, "Circuit breaker is open.");
    }

    /// <summary>Marks the activity as a failed execution and records the triggering exception.</summary>
    public static void MarkFailure(Activity? activity, Exception exception) {
        if(activity is null) {
            return;
        }

        activity.SetTag(Tags.Outcome, Outcomes.Failure);
        activity.AddException(exception);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>Marks the activity as having exceeded its configured deadline.</summary>
    public static void MarkTimeout(Activity? activity) {
        if(activity is null) {
            return;
        }

        activity.SetTag(Tags.Outcome, Outcomes.Timeout);
        activity.SetStatus(ActivityStatusCode.Error, "Operation exceeded the configured timeout.");
    }

    /// <summary>Marks the activity as abandoned because the caller cancelled.</summary>
    public static void MarkCancelled(Activity? activity) {
        activity?.SetTag(Tags.Outcome, Outcomes.Cancelled);
    }
}
