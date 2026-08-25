namespace Wiaoj.RateLimiting;

/// <summary>
/// Primary service for evaluating rate limit decisions against named and default policies.
/// </summary>
public interface IRateLimiter {
    /// <summary>
    /// Evaluates an acquisition against a specific named policy.
    /// </summary>
    /// <param name="policyName">The name of the registered rate limit policy.</param>
    /// <param name="key">The unique identity key.</param>
    /// <param name="cost">The operation cost.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The rate limit decision.</returns>
    ValueTask<RateLimitDecision> TryAcquireAsync(string policyName, string key, int cost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates an acquisition against the default configured policy.
    /// </summary>
    /// <param name="key">The unique identity key.</param>
    /// <param name="cost">The operation cost.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The rate limit decision.</returns>
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the underlying <see cref="IRateLimitAlgorithm"/> instance registered under the specified policy name.
    /// </summary>
    /// <param name="policyName">The name of the registered policy.</param>
    /// <returns>The configured rate limit algorithm instance.</returns>
    IRateLimitAlgorithm GetPolicy(string policyName);
}