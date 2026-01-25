namespace Wiaoj.BloomFilter;
/// <summary>
/// Defines the contract for storage providers responsible for persisting Bloom Filter snapshots.
/// Implementations can support various backends such as Redis, File System, Cloud Blob Storage, or SQL.
/// </summary>
public interface IBloomFilterStorage {
    /// <summary>
    /// Saves the serialized Bloom Filter data from the source stream to the persistent storage.
    /// </summary>
    /// <param name="filterName">The name of the filter, acting as the key for storage.</param>
    /// <param name="config">The configuration of the filter, which may be stored as metadata.</param>
    /// <param name="source">The stream containing the binary representation (header + bits) of the Bloom Filter.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    ValueTask SaveAsync(string filterName, BloomFilterConfiguration config, Stream source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the serialized Bloom Filter data as a stream from the persistent storage.
    /// </summary>
    /// <param name="filterName">The name of the filter to load.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous load operation. 
    /// Returns a tuple containing the configuration and the data stream if found; otherwise, <c>null</c>.
    /// </returns>
    ValueTask<(BloomFilterConfiguration Config, Stream DataStream)?> LoadStreamAsync(string filterName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string filterName, CancellationToken cancellationToken = default);
}