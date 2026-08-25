namespace Wiaoj.RateLimiting;

/// <summary>
/// Defines the core contract for rate limiting mathematical evaluation algorithms.
/// </summary>
public interface IRateLimitAlgorithm {
    /// <summary>
    /// Evaluates whether an operation with a specific cost is permitted for the specified key.
    /// </summary>
    /// <param name="key">The unique identity key being evaluated.</param>
    /// <param name="cost">The number of cost/token units requested.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The decision outcome of the evaluation.</returns>
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default);
}