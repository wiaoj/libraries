using System.Numerics;
using System.Runtime.CompilerServices;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Engine;

/// <summary>
/// Provides mathematical calculations for Bloom Filter sizing, capacity, and probability estimations.
/// </summary>
public static class BloomMath {
    /// <summary>
    /// Natural logarithm of 2: ln(2) ≈ 0.6931471805599453
    /// </summary>
    public const double Ln2 = 0.693147180559945309417232121458;

    /// <summary>
    /// Square of the natural logarithm of 2: (ln 2)^2 ≈ 0.4804530139182014
    /// </summary>
    public const double Ln2Squared = 0.480453013918201424667102526327;

    /// <summary>
    /// Calculates the optimal bit array size (m) for a target capacity and error rate.
    /// Formula: m = -(n * ln(p)) / (ln(2)^2)
    /// </summary>
    /// <param name="expectedItems">The expected number of items (n).</param>
    /// <param name="errorRate">The target false positive rate percentage (p).</param>
    /// <returns>The optimal size in bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CalculateOptimalBits(long expectedItems, Percentage errorRate) {
        double p = errorRate.Value;
        double m = -(expectedItems * Math.Log(p)) / Ln2Squared;
        return (long)Math.Ceiling(m);
    }

    /// <summary>
    /// Calculates the optimal number of hash functions (k) for a given bit size and item capacity.
    /// Formula: k = (m / n) * ln(2)
    /// </summary>
    /// <param name="sizeInBits">The total size of the bit array in bits (m).</param>
    /// <param name="expectedItems">The expected number of items (n).</param>
    /// <returns>The optimal number of hash functions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateOptimalHashCount(long sizeInBits, long expectedItems) {
        double k = ((double)sizeInBits / expectedItems) * Ln2;
        return Math.Max(1, (int)Math.Ceiling(k));
    }

    /// <summary>
    /// Estimates the current false positive probability based on fill ratio and hash function count.
    /// Formula: p ≈ (fillRatio)^k
    /// </summary>
    /// <param name="fillRatio">The saturation percentage of set bits.</param>
    /// <param name="hashFunctionCount">The number of hash functions (k).</param>
    /// <returns>The estimated false positive probability as a <see cref="Percentage"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Percentage EstimateFalsePositiveProbability(Percentage fillRatio, int hashFunctionCount) {
        double prob = Math.Pow(fillRatio.Value, hashFunctionCount);
        return Percentage.FromDouble(prob);
    }

    /// <summary>
    /// Estimates the approximate number of distinct items inserted into the filter based on set bits count.
    /// Formula: n* ≈ -(m / k) * ln(1 - X / m)
    /// </summary>
    /// <param name="sizeInBits">The total size of the bit array (m).</param>
    /// <param name="setBitsCount">The number of bits set to 1 (X).</param>
    /// <param name="hashFunctionCount">The number of hash functions (k).</param>
    /// <returns>The estimated number of inserted items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long EstimateInsertedItems(long sizeInBits, long setBitsCount, int hashFunctionCount) {
        if(setBitsCount <= 0) return 0;
        if(setBitsCount >= sizeInBits) return sizeInBits;

        double ratio = (double)setBitsCount / sizeInBits;
        double estimated = -((double)sizeInBits / hashFunctionCount) * Math.Log(1.0 - ratio);
        return (long)Math.Round(estimated);
    }

    /// <summary>
    /// Calculates the optimal power-of-two shard count required if total bytes exceed the configured threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateOptimalShardCount(long sizeInBits, long thresholdBytes) {
        if(thresholdBytes <= 0) return 1;

        long totalBytes = BitsToBytes(sizeInBits);
        if(totalBytes <= thresholdBytes) {
            return 1;
        }

        double ratio = (double)totalBytes / thresholdBytes;
        int needed = (int)Math.Ceiling(ratio);
        return Math.Max(2, (int)BitOperations.RoundUpToPowerOf2((uint)needed));
    }

    /// <summary>
    /// Calculates the minimum number of bytes required to hold the specified number of bits,
    /// rounding up to the nearest whole byte.
    /// Formula: (sizeInBits + 7) / 8
    /// </summary>
    /// <param name="sizeInBits">The total number of bits.</param>
    /// <returns>The total number of bytes required.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long BitsToBytes(long sizeInBits) {
        if(sizeInBits <= 0) return 0;
        return (sizeInBits + 7) / 8;
    }

    /// <summary>
    /// Calculates the minimum number of 64-bit words (ulong) required to hold the specified number of bits.
    /// Formula: (sizeInBits + 63) / 64
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitsToWordCount(long sizeInBits) {
        if(sizeInBits <= 0) return 0;
        return (int)((sizeInBits + 63) / 64);
    }
}