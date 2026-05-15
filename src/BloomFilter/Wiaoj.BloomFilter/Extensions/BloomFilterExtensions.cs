using System.IO.Hashing;
using System.Text.Unicode;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Extensions;

/// <summary>
/// Provides extension methods for <see cref="BloomFilterConfiguration"/>.
/// </summary>
public static class BloomFilterExtensions {
    /// <summary>
    /// Extension methods for <see cref="BloomFilterConfiguration"/>.
    /// </summary>
    extension(BloomFilterConfiguration configuration) {
        /// <summary>
        /// Calculates a unique fingerprint for the filter configuration based on its parameters and header version.
        /// </summary>
        /// <returns>A 64-bit unsigned integer representing the configuration's fingerprint.</returns>
        public ulong GetFingerprint() {
            ValueList<byte> buffer = new(); 
            Utf8.TryWrite(
                buffer,
                $"{configuration.ExpectedItems}|{configuration.ErrorRate}|{configuration.HashSeed}|v{BloomFilterHeader.Version}", 
                out int _);

            return XxHash3.HashToUInt64(buffer);
        }
    }
}