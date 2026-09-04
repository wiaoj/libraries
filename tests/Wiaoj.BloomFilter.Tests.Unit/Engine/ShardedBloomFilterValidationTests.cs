using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

// NOTE: This assumes shard-count validation throws ArgumentOutOfRangeException, either from
// WithShardCount() or from the ShardedBloomFilter constructor itself. Adjust the exception type
// and/or the point of failure (config.WithShardCount vs. `new ShardedBloomFilter(...)`) to match
// the real implementation if it differs.
public class ShardedBloomFilterValidationTests {
    private readonly BloomFilterContext _context;
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public ShardedBloomFilterValidationTests() {
        BloomFilterOptions options = new();
        this._context = new BloomFilterContext(
            Storage: new FakeBloomFilterStorage(),
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class ConstructorMethod : ShardedBloomFilterValidationTests {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Should_ThrowArgumentOutOfRangeException_When_ShardCountIsZeroOrNegative(int invalidShardCount) {
            // Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => {
                BloomFilterConfiguration config = this._configFactory
                    .Create(FilterName.Parse("invalid-shard-count"), 1_000, 0.01)
                    .WithShardCount(invalidShardCount);

                using ShardedBloomFilter filter = new(config, this._context);
            });
        }

        [Fact]
        public void Should_ThrowArgumentOutOfRangeException_When_ShardCountIsOne() {
            // A single-shard "sharded" filter is arguably a configuration mistake — verify the
            // library either rejects it outright or treat this as documentation of intended
            // behavior if 1 is meant to be allowed (adjust assertion if so).
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => {
                BloomFilterConfiguration config = this._configFactory
                    .Create(FilterName.Parse("single-shard-count"), 1_000, 0.01)
                    .WithShardCount(1);

                using ShardedBloomFilter filter = new(config, this._context);
            });
        }

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(12)]
        public void Should_ThrowArgumentException_When_ShardCountIsNotPowerOfTwo(int nonPowerOfTwoShards) {
            Assert.ThrowsAny<ArgumentException>(() => {
                BloomFilterConfiguration config = this._configFactory
                    .Create(FilterName.Parse("non-pow2-shards"), 1_000, 0.01)
                    .WithShardCount(nonPowerOfTwoShards);

                using ShardedBloomFilter filter = new(config, this._context);
            });
        }
    }
}