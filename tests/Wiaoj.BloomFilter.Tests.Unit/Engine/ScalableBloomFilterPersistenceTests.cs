using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class ScalableBloomFilterPersistenceTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    private BloomFilterContext CreateContext(FakeBloomFilterStorage storage) {
        BloomFilterOptions options = new();
        return new BloomFilterContext(
            Storage: storage,
            RecyclableMemoryStreamManager: new RecyclableMemoryStreamManager(),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class SingleLayerPersistence : ScalableBloomFilterPersistenceTests {
        [Fact]
        public async Task Should_PersistAndReload_SingleLayerStateAccurately() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("scalable-single-persist"), 10_000, 0.01);

            using(ScalableBloomFilter originalFilter = new(config, context)) {
                originalFilter.Add("key-1");
                originalFilter.Add("key-2");
                Assert.True(originalFilter.IsDirty);

                await originalFilter.SaveAsync(TestContext.Current.CancellationToken);
                Assert.False(originalFilter.IsDirty);
            }

            // Act: Rehydrate in a brand-new instance
            using ScalableBloomFilter reloadedFilter = new(config, context);
            await reloadedFilter.ReloadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(reloadedFilter.Contains("key-1"));
            Assert.True(reloadedFilter.Contains("key-2"));
            Assert.False(reloadedFilter.Contains("missing-key"));
        }
    }

    public sealed class MultiLayerPersistence : ScalableBloomFilterPersistenceTests {
        [Fact]
        public async Task Should_PersistAndReload_MultiLayerStateAccurately() {
            // Arrange: Initial capacity 200 with 50% saturation to easily trigger dynamic scale-up
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("scalable-multi-persist"), 200, 0.01);

            const int totalItems = 3_000;
            using(ScalableBloomFilter originalFilter = new(
                config,
                context,
                growthRate: GrowthRate.Double,
                saturationThreshold: Percentage.FromDouble(0.50))) {

                for(int i = 0; i < totalItems; i++) {
                    originalFilter.Add($"item-{i}");
                }

                // Act: Save all layers
                await originalFilter.SaveAsync(TestContext.Current.CancellationToken);

                // Verify multiple layers exist in storage (e.g. scalable-multi-persist and scalable-multi-persist_L1)
                Assert.True(storage.Exists("scalable-multi-persist"));
                Assert.True(storage.Exists("scalable-multi-persist_L1"));
            }

            // Act: Rehydrate fresh instance from storage
            using ScalableBloomFilter reloadedFilter = new(
                config,
                context,
                growthRate: GrowthRate.Double,
                saturationThreshold: Percentage.FromDouble(0.50));

            await reloadedFilter.ReloadAsync(TestContext.Current.CancellationToken);

            // Assert: All items across all historical layers must be found
            for(int i = 0; i < totalItems; i++) {
                Assert.True(reloadedFilter.Contains($"item-{i}"), $"Item item-{i} was missing after multi-layer rehydration.");
            }

            Assert.False(reloadedFilter.Contains("unrelated-item"));
        }

        [Fact]
        public async Task Should_OnlySaveDirtyLayers_When_SomeLayersAreUnmodified() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("scalable-dirty-check"), 1_000, 0.01);

            using ScalableBloomFilter filter = new(config, context);

            // Filter starts clean (no items added)
            Assert.False(filter.IsDirty);

            // Act: Save on a clean filter should do nothing
            await filter.SaveAsync(TestContext.Current.CancellationToken);
            Assert.False(storage.Exists("scalable-dirty-check"));

            // Add an item -> becomes dirty
            filter.Add("new-item");
            Assert.True(filter.IsDirty);

            await filter.SaveAsync(TestContext.Current.CancellationToken);
            Assert.False(filter.IsDirty);
            Assert.True(storage.Exists("scalable-dirty-check"));
        }

        [Fact]
        public async Task Should_RemainEmpty_When_StorageHasNoData() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("scalable-empty-storage"), 1_000, 0.01);

            using ScalableBloomFilter filter = new(config, context);

            // Act: Reloading from empty storage should not throw
            await filter.ReloadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(filter.Contains("any-key"));
            Assert.Equal(0, filter.GetPopCount());
        }
    }
}
