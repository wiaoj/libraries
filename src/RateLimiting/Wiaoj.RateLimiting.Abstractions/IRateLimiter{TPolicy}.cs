namespace Wiaoj.RateLimiting;

/// <summary>
/// A strongly-typed rate limiter wrapper scoped to a specific marker policy type.
/// </summary>
/// <typeparam name="TPolicy">The marker type representing the policy category.</typeparam>
public interface IRateLimiter<TPolicy> where TPolicy : notnull {
    /// <summary>
    /// Evaluates an acquisition against this strongly-typed policy with specific cost and cancellation support.
    /// </summary>
    /// <param name="key">The unique identity key.</param>
    /// <param name="cost">The operation cost.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The rate limit decision.</returns>
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default);
}