#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator.Behaviors;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Enforces a sliding-window rate limit for a request type.
/// <para>
/// If the limit is exceeded, a <see cref="RateLimitExceededException"/> is thrown immediately
/// without invoking the handler. Limits are tracked per-request-type, not per user/tenant.
/// </para>
/// <para>For per-user rate limiting, use a custom <see cref="IPipelineBehavior{TRequest,TResponse}"/> instead.</para>
/// </summary>
/// <example>
/// <code>
/// [RateLimit(maxRequests: 100, per: RateLimitWindow.Minute)]
/// public record SendEmailCommand(...) : ICommand&lt;Unit&gt;;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RateLimitAttribute : Attribute {
    /// <summary>Maximum number of requests allowed within the window.</summary>
    public int MaxRequests { get; }

    /// <summary>The sliding window duration.</summary>
    public RateLimitWindow Per { get; }

    /// <param name="maxRequests">Maximum requests in the window. Must be > 0.</param>
    /// <param name="per">The time window unit.</param>
    public RateLimitAttribute(int maxRequests, RateLimitWindow per = RateLimitWindow.Second) {
        Preca.ThrowIfNegativeOrZero(maxRequests);
        MaxRequests = maxRequests;
        Per = per;
    }

    internal TimeSpan Window => Per switch {
        RateLimitWindow.Second => TimeSpan.FromSeconds(1),
        RateLimitWindow.Minute => TimeSpan.FromMinutes(1),
        RateLimitWindow.Hour => TimeSpan.FromHours(1),
        _ => TimeSpan.FromSeconds(1)
    };
}