using System.IO.Hashing;
using System.Text.Unicode;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Extensions;

public static class BloomFilterExtensions {
    extension(BloomFilterConfiguration configuration) {
        public ulong GetFingerprint() {
            //string raw = $"{configuration.ExpectedItems}|{configuration.ErrorRate.Value}|{configuration.HashSeed}|v{BloomFilterHeader.Version}";
            //return XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(raw));

            ValueList<byte> buffer = new(); 
            Utf8.TryWrite(
                buffer,
                $"{configuration.ExpectedItems}|{configuration.ErrorRate.Value}|{configuration.HashSeed}|v{BloomFilterHeader.Version}", 
                out int _);

            return XxHash3.HashToUInt64(buffer);
        }
    }
}