using System.IO.Hashing;
using System.Text;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Extensions; 
public static class BloomFilterExtensions {
    extension(BloomFilterConfiguration configuration) {
        public ulong GetFingerprint() {
            string raw = $"{configuration.ExpectedItems}|{configuration.ErrorRate.Value}|{configuration.HashSeed}|v{BloomFilterHeader.Version}";
            return XxHash3.HashToUInt64(Encoding.UTF8.GetBytes(raw));
        }
    }
}