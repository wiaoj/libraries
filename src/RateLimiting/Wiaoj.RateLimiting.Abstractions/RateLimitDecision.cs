using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting;

/// <summary>
/// Represents the result of a rate limiting evaluation.
/// </summary>
/// <param name="IsAllowed">Indicates whether the request is allowed to proceed.</param>
/// <param name="RetryAfter">The duration to wait before retrying, if the request was denied.</param>
/// <param name="Remaining">The remaining request capacity or tokens for the key, if available.</param>
public readonly record struct RateLimitDecision(bool IsAllowed, TimeSpan? RetryAfter, long? Remaining) {

    /// <summary>
    /// Creates a decision indicating the request was permitted with unknown remaining capacity.
    /// </summary>
    /// <returns>An allowed <see cref="RateLimitDecision"/>.</returns>
    public static RateLimitDecision Allowed() {
        return new RateLimitDecision(true, null, null);
    }

    /// <summary>
    /// Creates a decision indicating the request was permitted with a known remaining capacity.
    /// </summary>
    /// <param name="remaining">The remaining capacity or tokens. Must be non-negative.</param>
    /// <returns>An allowed <see cref="RateLimitDecision"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="remaining"/> is negative.</exception>
    public static RateLimitDecision Allowed(long remaining) {
        Preca.ThrowIfNegative(remaining);
        return new RateLimitDecision(true, null, remaining);
    }

    /// <summary>
    /// Creates a decision indicating the request was denied with a retry duration.
    /// </summary>
    /// <param name="retryAfter">The duration to wait before retrying. Must be non-negative.</param>
    /// <returns>A denied <see cref="RateLimitDecision"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="retryAfter"/> is negative.</exception>
    public static RateLimitDecision Denied(TimeSpan retryAfter) {
        Preca.ThrowIfNegative(retryAfter);
        return new RateLimitDecision(false, retryAfter, 0);
    }

    /// <summary>
    /// Creates a decision indicating the request was denied with a retry duration and remaining capacity.
    /// </summary>
    /// <param name="retryAfter">The duration to wait before retrying. Must be non-negative.</param>
    /// <param name="remaining">The remaining capacity. Must be non-negative.</param>
    /// <returns>A denied <see cref="RateLimitDecision"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="retryAfter"/> or <paramref name="remaining"/> is negative.</exception>
    public static RateLimitDecision Denied(TimeSpan retryAfter, long remaining) {
        Preca.ThrowIfNegative(retryAfter);
        Preca.ThrowIfNegative(remaining);
        return new RateLimitDecision(false, retryAfter, remaining);
    }
}