namespace Wiaoj.DistributedCounter;
/// <summary>
/// Provides high-level operations and management for the distributed counter system.
/// This service acts as the primary entry point for batch queries and system-wide actions.
/// </summary>
public interface IDistributedCounterService {
    /// <summary>
    /// Efficiently retrieves the values of multiple counters in a single batch.
    /// </summary>
    /// <param name="counterNames">A collection of counter names to query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of counter values. Must be disposed to release pooled resources.</returns>
    ValueTask<CounterValueCollection> GetValuesAsync(IEnumerable<string> counterNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces all buffered counters to flush their pending increments to the remote storage immediately.
    /// Use this during graceful shutdowns or when manual consistency is required.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask FlushAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all registered counters across the entire system. 
    /// This clears local memory buffers and deletes the corresponding keys from the remote storage.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask ResetAllAsync(CancellationToken cancellationToken = default);
}