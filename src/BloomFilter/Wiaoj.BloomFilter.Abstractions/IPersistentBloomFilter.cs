namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents a stateful Bloom Filter that supports persistence operations (Save/Reload).
/// Extends the basic <see cref="IBloomFilter"/> functionality with storage synchronization capabilities.
/// </summary>
public interface IPersistentBloomFilter : IBloomFilter {
    /// <summary>
    /// Indicates whether the filter has changed since the last save.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Persists the current state of the Bloom Filter to the configured storage provider.
    /// </summary>
    /// <remarks>
    /// This operation typically involves thread-safe snapshotting of the bit array, 
    /// calculating checksums, and writing the data stream to a persistent medium (e.g., Redis, FileSystem).
    /// </remarks>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    ValueTask SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the Bloom Filter state from the storage provider, updating the in-memory bits.
    /// </summary>
    /// <remarks>
    /// This method replaces the current bit array with the data found in storage. 
    /// It is useful for synchronizing state in distributed environments or recovering from a restart.
    /// </remarks>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous reload operation.</returns>
    ValueTask ReloadAsync(CancellationToken cancellationToken = default);
}