namespace Wiaoj.BloomFilter;

/// <summary>
/// Defines a factory for creating optimal Bloom Filter configurations.
/// </summary>
public interface IBloomFilterConfigurationFactory {
    /// <summary>
    /// Creates an optimal configuration based on expected items and error rate.
    /// </summary>
    /// <param name="name">The name of the filter.</param>
    /// <param name="expectedItems">The number of items (n).</param>
    /// <param name="errorRate">The desired false positive rate (p).</param>
    /// <param name="hashSeed">Optional custom hash seed.</param>
    /// <returns>A calculated <see cref="BloomFilterConfiguration"/>.</returns>
    BloomFilterConfiguration Create(FilterName name, long expectedItems, double errorRate, long? hashSeed = null);
}
