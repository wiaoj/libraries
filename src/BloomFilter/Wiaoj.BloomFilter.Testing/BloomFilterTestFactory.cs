namespace Wiaoj.BloomFilter.Testing;

/// <summary>
/// Factory helpers for generating valid Bloom Filter domain models for testing.
/// </summary>
public static class BloomFilterTestFactory {
    /// <summary>
    /// Creates a valid default <see cref="BloomFilterConfiguration"/>.
    /// </summary>
    public static BloomFilterConfiguration CreateConfiguration(
        string name = "test-filter",
        long expectedItems = 10_000,
        double errorRate = 0.01,
        long sizeInBits = 95_851,
        int hashFunctions = 7,
        long hashSeed = 0x7769616F6A5F6266,
        int shardCount = 1) {
        return new BloomFilterConfiguration(
            FilterName.Parse(name),
            expectedItems,
            errorRate,
            sizeInBits,
            hashFunctions,
            hashSeed,
            shardCount);
    }
}