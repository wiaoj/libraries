namespace Wiaoj.DistributedCounter;
/// <summary>
/// Represents the result of a limit-checked counter operation.
/// </summary>
/// <param name="IsAllowed">True if the operation was within the limit and applied; otherwise, false.</param>
/// <param name="CurrentValue">The value of the counter after the operation (or the current value if rejected).</param>
/// <param name="Remaining">The remaining capacity until the limit is reached.</param>
public readonly record struct CounterLimitResult(bool IsAllowed, long CurrentValue, long Remaining);