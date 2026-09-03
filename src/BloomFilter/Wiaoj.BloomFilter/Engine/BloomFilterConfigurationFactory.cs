using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Internal implementation of the configuration factory that performs mathematical calculations.
/// </summary>
internal sealed class BloomFilterConfigurationFactory : IBloomFilterConfigurationFactory {
    private const long DefaultHashSeed = 0x7769616F6A5F6266;

    public BloomFilterConfiguration Create(FilterName name, long expectedItems, double errorRate, long? hashSeed = null) {
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate,
                                         BloomFilterConfiguration.MinimumErrorRate,
                                         BloomFilterConfiguration.MaximumErrorRate);


        long sizeInBits = BloomMath.CalculateOptimalBits(expectedItems, errorRate);
        int hashFunctionCount = BloomMath.CalculateOptimalHashCount(sizeInBits, expectedItems);

        return new BloomFilterConfiguration(
            name,
            expectedItems,
            errorRate,
            sizeInBits,
            hashFunctionCount,
            hashSeed ?? DefaultHashSeed);
    }
}
