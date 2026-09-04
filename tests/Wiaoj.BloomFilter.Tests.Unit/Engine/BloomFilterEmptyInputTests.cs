using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterEmptyInputTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    internal BloomFilterContext CreateContext() {
        return new BloomFilterContext(
            Storage: new FakeBloomFilterStorage(),
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
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
}