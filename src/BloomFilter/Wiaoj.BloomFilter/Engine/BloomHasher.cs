using System.Runtime.CompilerServices;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.BloomFilter.Engine;

/// <summary>
/// Mathematical hashing engine for Bloom Filters.
/// Implements the Kirsch-Mitzenmacher double hashing technique and Fast Modulo reduction.
/// </summary>
internal static class BloomHasher {
    /// <summary>
    /// Computes two independent 64-bit base hashes from a single seeded 128-bit hash execution.
    /// </summary>
    /// <param name="item">The byte span of the item to hash.</param>
    /// <param name="seed">The 64-bit hash seed.</param>
    /// <param name="h1">The lower 64-bit hash.</param>
    /// <param name="h2">The upper 64-bit hash.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeBaseHashes(ReadOnlySpan<byte> item, long seed, out ulong h1, out ulong h2) {
        (h1, h2) = XxHash128.Compute(item, seed);
        if(h2 == 0) {
            h2 = BloomMath.GoldenRatio64;
        }
    }

    /// <summary>
    /// Computes the bit index position for the i-th hash function using Fast Modulo reduction.
    /// Formula: (h1 + i * h2) mod sizeInBits
    /// </summary>
    /// <param name="h1">The lower 64-bit base hash.</param>
    /// <param name="h2">The upper 64-bit base hash.</param>
    /// <param name="index">The 0-based hash iteration index (i).</param>
    /// <param name="sizeInBits">The total size of the bit array in bits (m).</param>
    /// <returns>The calculated bit index position in the range [0, sizeInBits - 1].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetBitPosition(ulong h1, ulong h2, int index, long sizeInBits) {
        if(h2 == 0) {
            h2 = BloomMath.GoldenRatio64;
        }
        ulong combinedHash = h1 + ((ulong)index * h2);
        return (long)(((UInt128)combinedHash * (ulong)sizeInBits) >> 64);
    }
}