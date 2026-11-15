using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting;

/// <summary>
/// What a key may still spend under a policy, read without spending any of it.
/// </summary>
/// <param name="Limit">The cost the policy allows per window.</param>
/// <param name="Remaining">The cost the key may still spend now. Never negative, never above <paramref name="Limit"/>.</param>
/// <param name="ResetAfter">How long until the current window closes.</param>
public readonly record struct RateLimitHeadroom(long Limit, long Remaining, TimeSpan ResetAfter) {

    /// <summary>
    /// Creates a headroom reading, holding <paramref name="remaining"/> within zero and <paramref name="limit"/>.
    /// </summary>
    /// <param name="limit">The cost the policy allows per window. Must be positive.</param>
    /// <param name="remaining">The cost still available; clamped to <c>[0, limit]</c>.</param>
    /// <param name="resetAfter">How long until the current window closes. Must be non-negative.</param>
    /// <returns>A <see cref="RateLimitHeadroom"/>.</returns>
    public static RateLimitHeadroom Of(long limit, long remaining, TimeSpan resetAfter) {
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegative(resetAfter);
        return new RateLimitHeadroom(limit, Math.Clamp(remaining, 0, limit), resetAfter);
    }
}
