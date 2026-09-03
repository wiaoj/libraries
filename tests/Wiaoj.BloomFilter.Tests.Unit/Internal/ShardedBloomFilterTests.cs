using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

public class ShardedBloomFilterTests {
    private readonly BloomFilterContext _context;
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public ShardedBloomFilterTests() {
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

    public sealed class AddAndContainsMethods : ShardedBloomFilterTests {
        [Fact]
        public void Should_DistributeItemsAcrossShards_AndFindAllSuccessfully() {
            // Arrange
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("sharded-test"), 4_000, 0.01)
                .WithShardCount(4);

            using ShardedBloomFilter filter = new(config, this._context);
            byte[][] items = [
                "user-1"u8.ToArray(),
                "user-2"u8.ToArray(),
                "user-3"u8.ToArray(),
                "user-4"u8.ToArray()
            ];

            // Act
            foreach(byte[] item in items) {
                filter.Add(item);
            }

            // Assert
            foreach(byte[] item in items) {
                Assert.True(filter.Contains(item), $"Item '{Encoding.UTF8.GetString(item)}' should be found.");
            }

            Assert.True(filter.GetPopCount() >= items.Length);
            Assert.True(filter.IsDirty);
        }

        [Theory]
        [InlineData(0x7769616F6A5F6266)]
        [InlineData(0xDEADBEEFCAFE)]
        [InlineData(123456789)]
        [InlineData(-987654321)]
        public void Should_SupportCustomHashSeed_WithSharding(long customSeed) {
            // Arrange
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("sharded-seed"), 2_000, 0.01)
                .WithShardCount(4)
                .WithHashSeed(customSeed);

            using ShardedBloomFilter filter = new(config, this._context);
            string[] testItems = ["order-101", "order-102", "order-103", "order-104"];

            // Act & Assert
            foreach(string item in testItems) {
                filter.Add(item.AsSpan());
                Assert.True(filter.Contains(item.AsSpan()), $"Sharded filter failed to contain '{item}' with seed {customSeed:X}");
            }
        }
    }

    public sealed class PersistenceMethods : ShardedBloomFilterTests {
        [Fact]
        public async Task Should_SaveAllDirtyShards_ToStorage() {
            // Arrange
            InMemoryBloomFilterStorage storage = new();
            BloomFilterContext contextWithStorage = this._context with { Storage = storage };
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("sharded-persist"), 2_000, 0.01)
                .WithShardCount(2);

            using ShardedBloomFilter originalFilter = new(config, contextWithStorage);
            originalFilter.Add("test-element"u8);

            // Act
            await originalFilter.SaveAsync();

            // Assert: Verify that shard files exist in storage (e.g. sharded-persist_s0 or s1)
            Assert.False(originalFilter.IsDirty);
            Assert.True(storage.Exists("sharded-persist_s0") || storage.Exists("sharded-persist_s1"));
        }
    }
}