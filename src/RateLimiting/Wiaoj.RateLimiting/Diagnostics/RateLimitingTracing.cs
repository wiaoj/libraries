using System.Diagnostics;
using System.Reflection;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Distributed tracing ActivitySource provider for rate limiting operations.
/// </summary>
internal static class RateLimitingTracing {
    public const string SourceName = "Wiaoj.RateLimiting";

    public const string AcquireActivityName = "rate_limit.acquire";

    /// <summary>Policy name reported when the caller used the default-policy overload.</summary>
    public const string DefaultPolicyName = "Default";

    private static readonly string SourceVersion =
        typeof(RateLimitingTracing).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(RateLimitingTracing).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    public static readonly ActivitySource Source = new(SourceName, SourceVersion);

    /// <summary>Tag names applied to rate limiting activities.</summary>
    public static class Tags {
        public const string Policy = "rate_limit.policy";
        public const string Key = "rate_limit.key";
        public const string Cost = "rate_limit.cost";
        public const string Decision = "rate_limit.decision";
        public const string Remaining = "rate_limit.remaining";
        public const string RetryAfterMs = "rate_limit.retry_after_ms";
    }

    /// <summary>Values reported through the <see cref="Tags.Decision"/> tag.</summary>
    public static class Decisions {
        public const string Allowed = "allowed";
        public const string Denied = "denied";
    }

    /// <summary>
    /// Starts an acquire activity. Returns <see langword="null"/> when no listener is subscribed,
    /// in which case <see cref="RecordDecision"/> is a no-op.
    /// </summary>
    public static Activity? StartAcquire(string policy, string key, int cost) {
        Activity? activity = Source.StartActivity(AcquireActivityName, ActivityKind.Internal);

        if(activity is null) {
            return null;
        }

        activity.SetTag(Tags.Policy, policy);
        activity.SetTag(Tags.Key, key);
        activity.SetTag(Tags.Cost, cost);
        return activity;
    }

    /// <summary>
    /// Records the acquire outcome on the activity.
    /// </summary>
    /// <remarks>
    /// A denial is deliberately not an <see cref="ActivityStatusCode.Error"/>: exceeding a quota is the limiter
    /// working as designed and is returned as a value rather than thrown, so the operation did not fail.
    /// </remarks>
    public static void RecordDecision(Activity? activity, in RateLimitDecision decision) {
        if(activity is null) {
            return;
        }

        activity.SetTag(Tags.Decision, decision.IsAllowed ? Decisions.Allowed : Decisions.Denied);
        activity.SetTag(Tags.Remaining, decision.Remaining);

        if(!decision.IsAllowed && decision.RetryAfter.HasValue) {
            activity.SetTag(Tags.RetryAfterMs, (long)decision.RetryAfter.Value.TotalMilliseconds);
        }
    }
}
