namespace Wiaoj.DistributedCounter;
/// <summary>
/// Defines the low-level contract for distributed storage providers.
/// Implementations must ensure atomic operations.
/// </summary>
public interface ICounterStorage {
    /// <summary>
    /// Atomically increments a counter and optionally sets its expiration.
    /// </summary>
    ValueTask<CounterValue> AtomicIncrementAsync(
        CounterKey key,
        long amount,
        CounterExpiry expiry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to increment a counter only if the resulting value does not exceed a limit.
    /// </summary>
    ValueTask<CounterLimitResult> TryIncrementAsync(
        CounterKey key,
        long amount,
        long limit,
        CounterExpiry expiry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to decrement a counter only if the resulting value does not fall below a minimum limit.
    /// </summary>
    // DEĞİŞİKLİK: (minLimit, amount) -> (amount, minLimit) sıralaması yapıldı.
    ValueTask<CounterLimitResult> TryDecrementAsync(
        CounterKey key,
        long amount,
        long minLimit,
        CounterExpiry expiry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the current value of a counter from storage.
    /// </summary>
    ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves multiple counter values in a single batch request.
    /// </summary>
    ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(
        IEnumerable<CounterKey> keys,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves multiple counter values and fills the destination memory span.
    /// </summary>
    ValueTask GetManyAsync(
        ReadOnlyMemory<CounterKey> keys,
        Memory<CounterValue> destination,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a counter from the storage.
    /// </summary>
    ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Overwrites a counter with a specific value and expiration.
    /// </summary>
    ValueTask SetAsync(
        CounterKey key,
        CounterValue value,
        CounterExpiry expiry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Flushes a batch of counter updates to the storage in a single operation.
    /// </summary>
    ValueTask BatchIncrementAsync(
        ReadOnlyMemory<CounterUpdate> updates,
        Memory<long> resultDestination,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents a single increment/decrement operation within a batch.
/// </summary>
public readonly record struct CounterUpdate(CounterKey Key, long Amount, CounterExpiry Expiry);