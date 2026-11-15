namespace Wiaoj.RateLimiting;

/// <summary>
/// An algorithm whose remaining capacity can be read without consuming any of it.
/// </summary>
/// <remarks>
/// <see cref="IRateLimitAlgorithm.TryAcquireAsync"/> spends what it asks for, so reading headroom through it (to draw a
/// gauge, say) takes a request from the budget being reported. <see cref="PeekAsync"/> reads the same counters and writes
/// nothing, records no decision and counts no cost. Its answer is a snapshot: a concurrent acquire may spend it at once.
/// </remarks>
public interface IRateLimitHeadroom {
    /// <summary>
    /// Reads what <paramref name="key"/> may still spend, without spending.
    /// </summary>
    /// <param name="key">The identity key being read.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The key's headroom now.</returns>
    ValueTask<RateLimitHeadroom> PeekAsync(string key, CancellationToken cancellationToken = default);
}
