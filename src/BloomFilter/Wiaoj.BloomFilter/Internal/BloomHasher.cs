using System.IO.Hashing;
using System.Runtime.CompilerServices;

namespace Wiaoj.BloomFilter.Internal; 
/// <summary>
/// Encapsulates high-performance hashing mathematics for Bloom Filters, 
/// including the Kirsch-Mitzenmacher technique and Fast Modulo reduction.
/// </summary>
internal static class BloomHasher {

    /// <summary>
    /// Computes the two foundational 64-bit hashes required for the Kirsch-Mitzenmacher technique.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeBaseHashes(ReadOnlySpan<byte> item, long seed, out ulong h1, out ulong h2) {
        ulong hash64 = XxHash3.HashToUInt64(item, seed);
        h1 = hash64;
        h2 = (hash64 >> 32) | (hash64 << 32);
    }

    /// <summary>
    /// Computes the specific bit index for the i-th hash function using Fast Modulo reduction.
    /// </summary>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetBitPosition(ulong h1, ulong h2, int index, long sizeInBits) {
        ulong combinedHash = h1 + ((ulong)index * h2);

        // Fast Modulo: (hash * size) >> 64 is much faster than (hash % size)
        return (long)(((UInt128)combinedHash * (ulong)sizeInBits) >> 64);
    }
}