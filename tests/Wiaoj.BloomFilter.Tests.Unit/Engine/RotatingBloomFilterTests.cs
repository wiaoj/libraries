using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class RotatingBloomFilterTests {
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly BloomFilterConfigurationFactory _configFactory = new();
    private readonly BloomFilterContext _context;

    public RotatingBloomFilterTests() {
        this._fakeTimeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        BloomFilterOptions options = new();
        this._context = new BloomFilterContext(
            Storage: new FakeBloomFilterStorage(),
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: this._fakeTimeProvider,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class TimeSlidingWindow : RotatingBloomFilterTests {
        [Fact]
        public void Should_ExpireOldShards_When_TimeWindowAdvances() {
            // Arrange: 3 shards for a 3-day window (1 day per shard)
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("rotating-filter"), 3_000, 0.01);
            using RotatingBloomFilter rotatingFilter = new(
                config,
                this._context,
                windowSize: TimeSpan.FromDays(3),
                shardCount: 3);

            // Act - Day 1: Add item
            rotatingFilter.Add("day-1-event");
            Assert.True(rotatingFilter.Contains("day-1-event"));

            // Advance time by 2 days
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(2));
            rotatingFilter.Add("day-3-event");

            // Day 1 item should still be alive (within 3-day window)
            Assert.True(rotatingFilter.Contains("day-1-event"));
            Assert.True(rotatingFilter.Contains("day-3-event"));

            // Advance time by another 2 days (4 days total since Day 1)
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(2));

            // Assert: Day 1 item has rotated out; Day 3 item remains valid
            Assert.False(rotatingFilter.Contains("day-1-event"));
            Assert.True(rotatingFilter.Contains("day-3-event"));
        }

        [Fact]
        public void Should_CompletelyRotateAllShards_When_TimeAdvancesFarBeyondWindow() {
            // Arrange: 3-day window with 3 shards
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("rotating-leap"), 3_000, 0.01);
            using RotatingBloomFilter filter = new(config, this._context, windowSize: TimeSpan.FromDays(3), shardCount: 3);

            filter.Add("ancient-event");
            Assert.True(filter.Contains("ancient-event"));

            // Act: Advance time by 30 days (far beyond 3-day window)
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(30));

            // Assert: Ancient event is gone, new event works cleanly
            Assert.False(filter.Contains("ancient-event"));
            filter.Add("fresh-event");
            Assert.True(filter.Contains("fresh-event"));
        }

        [Fact]
        public void Should_ContinueOperation_When_StorageDeleteFailsDuringShardExpiration() {
            // Arrange: Context with storage that throws during deletion
            FaultyDeleteStorage faultyStorage = new();
            BloomFilterContext faultyContext = this._context with { Storage = faultyStorage };
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("rotating-fault"), 3_000, 0.01);

            using RotatingBloomFilter filter = new(config, faultyContext, windowSize: TimeSpan.FromDays(2), shardCount: 2);
            filter.Add("pre-fault-event");

            // Act: Advance time so active shard expires and triggers background deletion
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(3));

            // Assert: Filter must remain operational and not crash despite storage delete error
            filter.Add("post-fault-event");
            Assert.True(filter.Contains("post-fault-event"));
            Assert.False(filter.Contains("pre-fault-event"));
        }

        [Fact]
        public void Should_AccuratelyAggregatePopCount_AcrossShardsAndAfterRotation() {
            // Arrange
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("rotating-popcount"), 3_000, 0.01);
            using RotatingBloomFilter filter = new(config, this._context, windowSize: TimeSpan.FromDays(3), shardCount: 3);

            filter.Add("event-shard-1");
            long initialPopCount = filter.GetPopCount();
            Assert.True(initialPopCount > 0);

            // Advance 1 day and add to shard 2
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            filter.Add("event-shard-2");
            long secondPopCount = filter.GetPopCount();
            Assert.True(secondPopCount > initialPopCount);

            // Advance past shard 1's window
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(3));
            long rotatedPopCount = filter.GetPopCount();
            Assert.True(rotatedPopCount < secondPopCount);
        }
    }

    public sealed class ConstructorValidation : RotatingBloomFilterTests {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-5)]
        public void Should_ThrowArgumentOutOfRangeException_When_ShardCountIsLessThanOne(int invalidShardCount) {
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("invalid-rotating"), 1_000, 0.01);
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new RotatingBloomFilter(config, this._context, TimeSpan.FromHours(1), invalidShardCount));
        }

        [Fact]
        public void Should_ThrowArgumentNullException_When_ArgumentsAreNull() {
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("null-rotating"), 1_000, 0.01);
            Assert.ThrowsAny<ArgumentNullException>(() => new RotatingBloomFilter(null!, this._context, TimeSpan.FromHours(1), 2));
            Assert.ThrowsAny<ArgumentNullException>(() => new RotatingBloomFilter(config, null!, TimeSpan.FromHours(1), 2));
        }
    }

    private sealed class FaultyDeleteStorage : IBloomFilterStorage {
        public Task<bool> SaveAsync(FilterName filterName, BloomFilterConfiguration config, Stream source, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public ValueTask<(BloomFilterConfiguration? Config, Stream DataStream)?> LoadStreamAsync(FilterName filterName, CancellationToken cancellationToken = default) => ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>(null);
        public Task DeleteAsync(FilterName filterName, CancellationToken cancellationToken = default) {
            throw new IOException("Simulated storage delete error");
        }
    }
}