using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class ShardedBloomFilterPersistenceTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    private BloomFilterContext CreateContext(FakeBloomFilterStorage storage) {
        BloomFilterOptions options = new();
        return new BloomFilterContext(
            Storage: storage,
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class ReloadMethod : ShardedBloomFilterPersistenceTests {
        [Fact]
        public async Task Should_RestoreAllShardData_When_ReloadedFromStorage_AfterSave() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = this.CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("sharded-reload"), 4_000, 0.01)
                .WithShardCount(4);

            using ShardedBloomFilter originalFilter = new(config, context);
            string[] items = ["reload-user-1", "reload-user-2", "reload-user-3", "reload-user-4", "reload-user-5"];
            foreach(string item in items) {
                originalFilter.Add(item);
            }

            // Act: persist every dirty shard, then rehydrate a brand-new instance from the
            // same storage backend to prove the data actually survives a save/reload cycle.
            await originalFilter.SaveAsync();

            using ShardedBloomFilter reloadedFilter = new(config, context);
            await reloadedFilter.ReloadAsync();

            // Assert: every item survives the round trip, spread across whichever shard it landed on
            foreach(string item in items) {
                Assert.True(reloadedFilter.Contains(item), $"Item '{item}' was lost during sharded save/reload round trip.");
            }

            Assert.False(reloadedFilter.Contains("item-that-was-never-added"));
            Assert.Equal(originalFilter.GetPopCount(), reloadedFilter.GetPopCount());
        }

        [Fact]
        public async Task Should_LeaveNewInstanceEmpty_When_NoDataWasEverSaved() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = this.CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("sharded-never-saved"), 2_000, 0.01)
                .WithShardCount(2);

            using ShardedBloomFilter filter = new(config, context);

            // Act
            await filter.ReloadAsync();

            // Assert: reload against empty storage should not throw and should leave the filter empty
            Assert.False(filter.Contains("anything"));
            Assert.Equal(0, filter.GetPopCount());
        }
    }
}