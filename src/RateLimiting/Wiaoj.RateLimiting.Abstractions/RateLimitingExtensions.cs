using System.Runtime.CompilerServices;

namespace Wiaoj.RateLimiting;

/// <summary>
/// Convenience extension methods for rate limiting operations.
/// </summary>
public static class RateLimitingExtensions {

    // --- IRateLimitAlgorithm Extensions ---

    /// <summary>Evaluates an acquisition with a cost of 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<RateLimitDecision> TryAcquireAsync(
        this IRateLimitAlgorithm algorithm,
        string key,
        CancellationToken cancellationToken = default) {
        return algorithm.TryAcquireAsync(key, 1, cancellationToken);
    }

    // --- IRateLimiter Extensions (Named Policy) ---

    /// <summary>Evaluates an acquisition against a specific named policy with a cost of 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<RateLimitDecision> TryAcquireAsync(
        this IRateLimiter limiter,
        string policyName,
        string key,
        CancellationToken cancellationToken = default) {
        return limiter.TryAcquireAsync(policyName, key, 1, cancellationToken);
    }

    // --- IRateLimiter Extensions (Default Policy) ---

    /// <summary>Evaluates an acquisition against the default policy with a cost of 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<RateLimitDecision> TryAcquireAsync(
        this IRateLimiter limiter,
        string key,
        CancellationToken cancellationToken = default) {
        return limiter.TryAcquireAsync(key, 1, cancellationToken);
    }

    // --- IRateLimiter<TPolicy> Extensions ---

    /// <summary>Evaluates an acquisition against this strongly-typed policy with a cost of 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<RateLimitDecision> TryAcquireAsync<TPolicy>(
        this IRateLimiter<TPolicy> limiter,
        string key,
        CancellationToken cancellationToken = default) where TPolicy : notnull {
        return limiter.TryAcquireAsync(key, 1, cancellationToken);
    }
}