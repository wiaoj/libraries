using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.BloomFilter.Advanced;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

// NOTE: These tests assume RotatingBloomFilter exposes SaveAsync()/ReloadAsync() the same way
// InMemoryBloomFilter and ShardedBloomFilter do. If RotatingBloomFilter persists per-shard under
// different key names, adjust the InMemoryBloomFilterStorage assertions accordingly.
public class RotatingBloomFilterPersistenceTests {
    private readonly FakeTimeProvider _fakeTimeProvider = new();
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public RotatingBloomFilterPersistenceTests() {
        this._fakeTimeProvider.SetUtcNow(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private BloomFilterContext CreateContext(InMemoryBloomFilterStorage storage) {
        BloomFilterOptions options = new();
        return new BloomFilterContext(
            Storage: storage,
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: this._fakeTimeProvider,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class ReloadMethod : RotatingBloomFilterPersistenceTests {
        [Fact]
        public async Task Should_PreserveActiveShardData_When_ReloadedFromStorage_AfterSave() {
            // Arrange
            InMemoryBloomFilterStorage storage = new();
            BloomFilterContext context = this.CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("rotating-reload"), 3_000, 0.01);

            using RotatingBloomFilter originalFilter = new(
                config,
                context,
                windowSize: TimeSpan.FromDays(3),
                shardCount: 3);

            originalFilter.Add("rotating-persisted-event");

            // Act
            await originalFilter.SaveAsync();

            using RotatingBloomFilter reloadedFilter = new(
                config,
                context,
                windowSize: TimeSpan.FromDays(3),
                shardCount: 3);
            await reloadedFilter.ReloadAsync();

            // Assert
            Assert.True(reloadedFilter.Contains("rotating-persisted-event"));
            Assert.False(reloadedFilter.Contains("never-added-event"));
        }

        [Fact]
        public async Task Should_NotResurrectExpiredShards_When_ReloadedAfterWindowHasPassed() {
            // Arrange
            InMemoryBloomFilterStorage storage = new();
            BloomFilterContext context = this.CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("rotating-expired-reload"), 3_000, 0.01);

            using(RotatingBloomFilter originalFilter = new(config, context, windowSize: TimeSpan.FromDays(3), shardCount: 3)) {
                originalFilter.Add("day-1-event");
                await originalFilter.SaveAsync();
            }

            // Act: move well past the configured window before reloading
            this._fakeTimeProvider.Advance(TimeSpan.FromDays(10));

            using RotatingBloomFilter reloadedFilter = new(config, context, windowSize: TimeSpan.FromDays(3), shardCount: 3);
            await reloadedFilter.ReloadAsync();

            // Assert: the previously-saved event should no longer be considered valid
            Assert.False(reloadedFilter.Contains("day-1-event"));
        }
    }
}