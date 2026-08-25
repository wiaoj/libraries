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
    /// <param name="remaining">The remaining capacity or tokens.</param>
    /// <returns>An allowed <see cref="RateLimitDecision"/>.</returns>
    public static RateLimitDecision Allowed(long remaining) {
        return new RateLimitDecision(true, null, remaining);
    }

    /// <summary>
    /// Creates a decision indicating the request was denied with a retry duration.
    /// </summary>
    /// <param name="retryAfter">The duration to wait before retrying.</param>
    /// <returns>A denied <see cref="RateLimitDecision"/>.</returns>
    public static RateLimitDecision Denied(TimeSpan retryAfter) {
        return new RateLimitDecision(false, retryAfter, 0);
    }

    /// <summary>
    /// Creates a decision indicating the request was denied with a retry duration and remaining capacity.
    /// </summary>
    /// <param name="retryAfter">The duration to wait before retrying.</param>
    /// <param name="remaining">The remaining capacity.</param>
    /// <returns>A denied <see cref="RateLimitDecision"/>.</returns>
    public static RateLimitDecision Denied(TimeSpan retryAfter, long remaining) {
        return new RateLimitDecision(false, retryAfter, remaining);
    }
}