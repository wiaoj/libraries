namespace Wiaoj.BloomFilter;

/// <summary>
/// Defines a factory contract for calculating mathematically optimal Bloom Filter configurations.
/// </summary>
public interface IBloomFilterConfigurationFactory {
    /// <summary>
    /// Creates an optimal configuration with an explicit custom hash seed.
    /// </summary>
    /// <param name="name">The unique name identifier of the filter.</param>
    /// <param name="expectedItems">The expected number of items (n).</param>
    /// <param name="errorRate">The desired false positive rate (p), strictly between 0 and 1.</param>
    /// <param name="hashSeed">The explicit 64-bit seed for hash calculations.</param>
    /// <returns>A calculated <see cref="BloomFilterConfiguration"/> instance.</returns>
    BloomFilterConfiguration Create(FilterName name, long expectedItems, double errorRate, long hashSeed);

    /// <summary>
    /// Creates an optimal configuration using the default engine hash seed.
    /// </summary>
    /// <param name="name">The unique name identifier of the filter.</param>
    /// <param name="expectedItems">The expected number of items (n).</param>
    /// <param name="errorRate">The desired false positive rate (p), strictly between 0 and 1.</param>
    /// <returns>A calculated <see cref="BloomFilterConfiguration"/> instance.</returns>
    BloomFilterConfiguration Create(FilterName name, long expectedItems, double errorRate);
}