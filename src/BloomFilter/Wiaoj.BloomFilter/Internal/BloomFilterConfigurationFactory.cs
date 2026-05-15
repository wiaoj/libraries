using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Internal;

/// <summary>
/// Internal implementation of the configuration factory that performs mathematical calculations.
/// </summary>
internal sealed class BloomFilterConfigurationFactory : IBloomFilterConfigurationFactory {
    private const long DefaultHashSeed = 0x7769616F6A5F6266;

    public BloomFilterConfiguration Create(FilterName name, long expectedItems, double errorRate, long? hashSeed = null) {
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfOutOfRange(errorRate, 0.0, 1.0, () => new ArgumentException("Error rate must be between 0 and 1."));

        // Formula for optimal bit size (m): m = -(n * ln(p)) / (ln(2)^2)
        double m = -(expectedItems * Math.Log(errorRate)) / Math.Pow(Math.Log(2), 2);
        long sizeInBits = (long)Math.Ceiling(m);

        // Formula for optimal hash functions (k): k = (m / n) * ln(2)
        double k = (m / expectedItems) * Math.Log(2);
        int hashFunctionCount = (int)Math.Ceiling(k);

        return new BloomFilterConfiguration(
            name,
            expectedItems,
            errorRate,
            sizeInBits,
            hashFunctionCount,
            hashSeed ?? DefaultHashSeed);
    }
}
