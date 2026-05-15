namespace Wiaoj.BloomFilter;

/// <summary>
/// Defines the management and monitoring services for Bloom Filters.
/// Provides methods for obtaining statistics, forcing persistence, and managing filter state.
/// </summary>
public interface IBloomFilterService {
    /// <summary>
    /// Retrieves a collection of statistics for all currently active Bloom Filters.
    /// </summary>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary where the key is the filter name and the value contains its high-level statistics.</returns>
    ValueTask<IReadOnlyDictionary<FilterName, BloomFilterStats>> GetAllStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves detailed, low-level metrics for a specific Bloom Filter.
    /// </summary>
    /// <param name="name">The name of the filter to query.</param>
    /// <returns>A record containing detailed memory usage, fill ratio, and probability metrics.</returns>
    ValueTask<BloomFilterDetailedStats> GetDetailedStatsAsync(FilterName name);

    /// <summary>
    /// Completely removes a filter from both memory and the persistent storage provider.
    /// </summary>
    /// <param name="name">The name of the filter to delete.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the completion of the deletion operation.</returns>
    ValueTask DeleteFilterAsync(FilterName name, CancellationToken ct = default);

    /// <summary>
    /// Forces an immediate save (flush) of all filters that have pending changes (dirty state).
    /// </summary>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the completion of the global save operation.</returns>
    ValueTask SaveAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Triggers a reload of a specific filter from the persistent storage, synchronized with memory.
    /// </summary>
    /// <param name="name">The name of the filter to reload.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the completion of the reload operation.</returns>
    ValueTask ReloadFilterAsync(FilterName name, CancellationToken ct = default);
}

/// <summary>
/// Represents high-level summary statistics for a Bloom Filter.
/// </summary>
/// <param name="Name">The name of the filter.</param>
/// <param name="ExpectedItems">The configured expected item count (n).</param>
/// <param name="ConfiguredErrorRate">The configured target false positive probability (p).</param>
/// <param name="SizeInBits">The total size of the bit array in bits (m).</param>
/// <param name="HashFunctions">The number of hash functions used (k).</param>
/// <param name="SetBitsCount">The actual number of bits currently set to 1.</param>
/// <param name="FillRatio">Percentage of bits that are set (0.0 to 1.0).</param>
/// <param name="IsHealthy">Indicates if the filter integrity is verified.</param>
public sealed record BloomFilterStats(
    string Name,
    long ExpectedItems,
    double ConfiguredErrorRate,
    long SizeInBits,
    int HashFunctions,
    long SetBitsCount,
    double FillRatio,
    bool IsHealthy
); 

/// <summary>
/// Represents detailed architectural and runtime metrics for a Bloom Filter.
/// </summary>
/// <param name="Name">The name of the filter.</param>
/// <param name="TotalBits">Total bit capacity.</param>
/// <param name="SetBits">Number of bits set to 1.</param>
/// <param name="FillRatio">Current saturation level.</param>
/// <param name="HashFunctions">Number of hash iterations.</param>
/// <param name="CurrentFalsePositiveProbability">Mathematically estimated false positive probability based on current fill ratio.</param>
/// <param name="MemoryUsageBytes">The estimated heap memory footprint in bytes.</param>
public sealed record BloomFilterDetailedStats(
    string Name,
    long TotalBits,
    long SetBits,
    double FillRatio,
    int HashFunctions,
    double CurrentFalsePositiveProbability,
    long MemoryUsageBytes
);
