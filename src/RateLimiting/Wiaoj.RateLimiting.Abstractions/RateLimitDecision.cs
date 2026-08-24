using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting;

/// <summary>
/// Represents the outcome of a <see cref="IRateLimitAlgorithm.TryAcquireAsync"/> call.
/// </summary>
/// <remarks>
/// Constructed exclusively through <see cref="Allowed"/> and <see cref="Denied"/> to keep the two
/// valid states (allowed vs. denied) from drifting apart — e.g. an "allowed" decision can never
/// accidentally carry a <see cref="RetryAfter"/> value.
/// </remarks>
public readonly record struct RateLimitDecision {
    /// <summary>Gets a value indicating whether the operation is allowed to proceed.</summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the duration the caller should wait before retrying, when <see cref="IsAllowed"/> is <see langword="false"/>.
    /// <see langword="null"/> when <see cref="IsAllowed"/> is <see langword="true"/>.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the number of remaining units within the current window, if the algorithm tracks and exposes it.
    /// <see langword="null"/> when the underlying algorithm does not report remaining capacity.
    /// </summary>
    public long? Remaining { get; }

    private RateLimitDecision(bool isAllowed, TimeSpan? retryAfter, long? remaining) {
        this.IsAllowed = isAllowed;
        this.RetryAfter = retryAfter;
        this.Remaining = remaining;
    }

    /// <summary>Creates a decision indicating the operation is allowed to proceed.</summary>
    /// <param name="remaining">Optional remaining capacity within the current window.</param>
    public static RateLimitDecision Allowed(long? remaining = null) {
        return new RateLimitDecision(true, retryAfter: null, remaining);
    }

    /// <summary>Creates a decision indicating the operation is denied.</summary>
    /// <param name="retryAfter">How long the caller should wait before retrying. Must be a non-negative duration.</param>
    /// <param name="remaining">Optional remaining capacity within the current window (typically <c>0</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="retryAfter"/> is negative.</exception>
    public static RateLimitDecision Denied(TimeSpan retryAfter, long? remaining = null) {
        Preca.ThrowIfNegative(
            retryAfter,
            static (param) => new ArgumentOutOfRangeException(nameof(retryAfter), "Retry-after duration must be non-negative."),
            nameof(retryAfter));
        return new RateLimitDecision(false, retryAfter, remaining);
    }
}