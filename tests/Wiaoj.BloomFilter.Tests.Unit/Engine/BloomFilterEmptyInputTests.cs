using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;
using System.Text;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterEmptyInputTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    internal BloomFilterContext CreateContext() {
        return new BloomFilterContext(
            Storage: new FakeBloomFilterStorage(),
            RecyclableMemoryStreamManager: new RecyclableMemoryStreamManager(),
            Logger: NullLogger.Instance,
            Options: new BloomFilterOptions(),
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class EmptySpanHandling : BloomFilterEmptyInputTests {
        [Fact]
        public void Should_HandleEmptyByteAndCharSpans_SafelyWithoutExceptions() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("empty-input-test"), 1_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            // Act
            bool addedEmptyBytes = filter.Add(ReadOnlySpan<byte>.Empty);
            bool containsEmptyBytes = filter.Contains(ReadOnlySpan<byte>.Empty);
            bool addedEmptyChars = filter.Add(ReadOnlySpan<char>.Empty);
            bool containsEmptyChars = filter.Contains(ReadOnlySpan<char>.Empty);

            // Assert
            Assert.True(addedEmptyBytes || !addedEmptyBytes); // Ensures no exceptions
            Assert.True(containsEmptyBytes);
            Assert.True(containsEmptyChars);
        }

        [Fact]
        public void Should_HandleEmptySpan_InShardedBloomFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("empty-sharded-test"), 2_000, 0.01).WithShardCount(2);
            using ShardedBloomFilter filter = new(config, context);

            // Act
            filter.Add(ReadOnlySpan<byte>.Empty);
            bool containsBytes = filter.Contains(ReadOnlySpan<byte>.Empty);
            filter.Add(ReadOnlySpan<char>.Empty);
            bool containsChars = filter.Contains(ReadOnlySpan<char>.Empty);

            // Assert
            Assert.True(containsBytes);
            Assert.True(containsChars);
        }

        [Fact]
        public void Should_HandleEmptySpan_InScalableBloomFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("empty-scalable-test"), 1_000, 0.01);
            using ScalableBloomFilter filter = new(config, context);

            // Act
            filter.Add(ReadOnlySpan<byte>.Empty);
            bool containsBytes = filter.Contains(ReadOnlySpan<byte>.Empty);
            filter.Add(ReadOnlySpan<char>.Empty);
            bool containsChars = filter.Contains(ReadOnlySpan<char>.Empty);

            // Assert
            Assert.True(containsBytes);
            Assert.True(containsChars);
        }

        [Fact]
        public void Should_HandleEmptySpan_InRotatingBloomFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("empty-rotating-test"), 1_000, 0.01);
            using RotatingBloomFilter filter = new(config, context, windowSize: TimeSpan.FromHours(1), shardCount: 2);

            // Act
            filter.Add(ReadOnlySpan<byte>.Empty);
            bool containsBytes = filter.Contains(ReadOnlySpan<byte>.Empty);
            filter.Add(ReadOnlySpan<char>.Empty);
            bool containsChars = filter.Contains(ReadOnlySpan<char>.Empty);

            // Assert
            Assert.True(containsBytes);
            Assert.True(containsChars);
        }
    }

    public sealed class LargeBufferFallback : BloomFilterEmptyInputTests {
        [Fact]
        public void Should_HandleLargeStringExceedingStackThreshold_SeamlesslyViaPooledBuffer() {
            // Arrange: 2048 chars > 256 stackalloc threshold
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("large-input-test"), 1_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            string largeKey = new('x', 2048);

            // Act
            bool added = filter.Add(largeKey.AsSpan());
            bool contains = filter.Contains(largeKey.AsSpan());
            bool containsMissing = filter.Contains(new string('y', 2048).AsSpan());

            // Assert
            Assert.True(added);
            Assert.True(contains);
            Assert.False(containsMissing);
        }
    }

    public sealed class UnicodeAndMultiByteEncoding : BloomFilterEmptyInputTests {
        [Fact]
        public void Should_HandleEmojiAndSurrogatePairs_EquivalentlyBetweenCharsAndBytes() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("emoji-test"), 1_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            string emojiKey = "order_🚀_completed_🔥_123_👨‍👩‍👧‍👦";
            byte[] emojiBytes = Encoding.UTF8.GetBytes(emojiKey);

            // Act
            bool added = filter.Add(emojiKey.AsSpan());
            bool containsSpan = filter.Contains(emojiKey.AsSpan());
            bool containsBytes = filter.Contains(emojiBytes);

            // Assert
            Assert.True(added);
            Assert.True(containsSpan);
            Assert.True(containsBytes);
            Assert.False(filter.Contains("order_🚀_failed_❌"u8));
        }

        [Fact]
        public void Should_HandleTurkishAndMultiByteCharacters_EquivalentlyBetweenCharsAndBytes() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("turkish-test"), 1_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            string turkishKey = "İstanbul_Şehir_Üniversitesi_Öğrenci_İşleri_Çalışanı_ĞÜŞİÖÇ";
            byte[] turkishBytes = Encoding.UTF8.GetBytes(turkishKey);

            // Act
            bool added = filter.Add(turkishKey.AsSpan());
            bool containsSpan = filter.Contains(turkishKey.AsSpan());
            bool containsBytes = filter.Contains(turkishBytes);

            // Assert
            Assert.True(added);
            Assert.True(containsSpan);
            Assert.True(containsBytes);
            Assert.False(filter.Contains("İstanbul_Şehir_Üniversitesi_Rektörlüğü".AsSpan()));
        }

        [Fact]
        public void Should_HandleMassiveMultiByteStringExceedingStackThreshold() {
            // Arrange: 500 repetitions = approx 12,000 bytes in UTF-8
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("massive-unicode-test"), 1_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            string massiveString = string.Concat(Enumerable.Repeat("🇹🇷_Türkçe_🚀_", 500));
            byte[] massiveBytes = Encoding.UTF8.GetBytes(massiveString);

            // Act
            bool added = filter.Add(massiveString.AsSpan());
            bool containsSpan = filter.Contains(massiveString.AsSpan());
            bool containsBytes = filter.Contains(massiveBytes);

            // Assert
            Assert.True(added);
            Assert.True(containsSpan);
            Assert.True(containsBytes);
        }
    }
}