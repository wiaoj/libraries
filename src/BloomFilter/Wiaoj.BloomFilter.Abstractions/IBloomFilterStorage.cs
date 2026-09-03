namespace Wiaoj.BloomFilter;

/// <summary>
/// Defines the contract for persistence storage providers responsible for saving and loading Bloom Filter snapshots.
/// Implementations can support various storage mediums such as File System, Redis, PostgreSQL, or Cloud Storage.
/// </summary>
public interface IBloomFilterStorage {
    /// <summary>
    /// Saves the serialized Bloom Filter stream to the underlying persistent storage.
    /// </summary>
    /// <param name="filterName">The strongly-typed name identifier of the filter.</param>
    /// <param name="config">The immutable configuration parameters of the filter.</param>
    /// <param name="source">The readable stream containing the binary representation (header and bit array).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> returning <see langword="true"/> if the snapshot was successfully committed;
    /// otherwise, <see langword="false"/> (e.g. when non-fatal storage errors are ignored).
    /// </returns>
    Task<bool> SaveAsync(
        FilterName filterName,
        BloomFilterConfiguration config,
        Stream source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the serialized Bloom Filter data stream from the persistent storage.
    /// </summary>
    /// <param name="filterName">The strongly-typed name identifier of the filter to load.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task returning the data stream and optional configuration metadata if found; otherwise, <see langword="null"/>.
    /// </returns>
    ValueTask<(BloomFilterConfiguration? Config, Stream DataStream)?> LoadStreamAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the persistent snapshot data associated with the specified filter name.
    /// </summary>
    /// <param name="filterName">The strongly-typed name identifier of the filter to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous deletion operation.</returns>
    Task DeleteAsync(FilterName filterName, CancellationToken cancellationToken = default);
}