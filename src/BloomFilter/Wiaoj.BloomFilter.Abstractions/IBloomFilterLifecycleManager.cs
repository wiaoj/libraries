namespace Wiaoj.BloomFilter;
/// <summary>
/// Defines management operations for Bloom Filters, such as saving state or reloading.
/// Used by background services and management APIs.
/// </summary>
public interface IBloomFilterLifecycleManager {
    /// <summary>
    /// Saves all filters that have changed since the last persistence operation.
    /// </summary>
    Task SaveAllDirtyAsync(CancellationToken cancellationToken = default);
}