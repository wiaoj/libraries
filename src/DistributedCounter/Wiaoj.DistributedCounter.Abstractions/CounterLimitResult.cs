namespace Wiaoj.DistributedCounter;

/// <summary>
/// Represents the result of a limit-checked counter operation.
/// </summary>
/// <param name="IsAllowed">True if the operation was within the limit and applied; otherwise, false.</param>
/// <param name="CurrentValue">The value of the counter after the operation (or the current value if rejected).</param>
/// <param name="Remaining">The remaining capacity until the limit is reached.</param>
/// <param name="Ttl">
/// The counter's remaining time-to-live at the moment of this operation, if known.
/// <see langword="null"/> when the counter has no expiry (<see cref="CounterExpiry.Infinite"/>)
/// or the storage backend couldn't determine it. Callers needing "when does this window reset"
/// (e.g. rate limiters computing <c>RetryAfter</c>) should use this instead of a separate
/// <see cref="ICounterStorage.GetTtlAsync"/> round-trip.
/// </param>
public readonly record struct CounterLimitResult(bool IsAllowed, long CurrentValue, long Remaining, TimeSpan? Ttl);