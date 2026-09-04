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
    }
}