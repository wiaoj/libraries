using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Advanced;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

public class ScalableBloomFilterTests {
    private readonly BloomFilterContext _context;
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public ScalableBloomFilterTests() {
        BloomFilterOptions options = new();
        this._context = new BloomFilterContext(
            Storage: new InMemoryBloomFilterStorage(),
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class DynamicScalingMethod : ScalableBloomFilterTests {
        [Fact]
        public void Should_ScaleAndRetainAllInsertedItems_WhenCapacityIsExceeded() {
            // Arrange: Small initial capacity to force scaling
            BloomFilterConfiguration baseConfig = this._configFactory.Create(FilterName.Parse("scalable-test"), 1_000, 0.01);
            using ScalableBloomFilter scalableFilter = new(
                baseConfig,
                this._context,
                growthRate: GrowthRate.Double,
                saturationThreshold: Percentage.FromDouble(0.50));

            const int itemsToInsert = 10_000;

            // Act
            for(int i = 0; i < itemsToInsert; i++) {
                scalableFilter.Add($"item-sequence-{i}");
            }

            // Assert: All items must be present across all dynamically scaled layers
            for(int i = 0; i < itemsToInsert; i++) {
                Assert.True(scalableFilter.Contains($"item-sequence-{i}"));
            }

            Assert.False(scalableFilter.Contains("item-that-does-not-exist"));
        }

        [Fact]
        public void Should_ReturnFalseOnAdd_When_ItemAlreadyExistsInAnyLayer() {
            // Arrange
            BloomFilterConfiguration baseConfig = this._configFactory.Create(FilterName.Parse("scalable-dup"), 1_000, 0.01);
            using ScalableBloomFilter filter = new(baseConfig, this._context);

            filter.Add("unique-key");

            // Act
            bool secondAdd = filter.Add("unique-key");

            // Assert
            Assert.False(secondAdd);
        }
    }
}