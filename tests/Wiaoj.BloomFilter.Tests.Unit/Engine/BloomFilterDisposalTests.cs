using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterDisposalTests {
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

    public sealed class DisposedStateGuards : BloomFilterDisposalTests {
        [Fact]
        public void Should_ThrowObjectDisposedException_When_AddingOrQueryingDisposedInMemoryFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("disposed-in-memory"), 1_000, 0.01);
            InMemoryBloomFilter filter = new(config, context);

            // Act
            filter.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => filter.Add("key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Add("key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.GetPopCount());
        }

        [Fact]
        public void Should_ThrowObjectDisposedException_When_AddingOrQueryingDisposedShardedFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("disposed-sharded"), 2_000, 0.01).WithShardCount(2);
            ShardedBloomFilter filter = new(config, context);

            // Act
            filter.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => filter.Add("shard-key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Add("shard-key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("shard-key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("shard-key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.GetPopCount());
        }

        [Fact]
        public void Should_ThrowObjectDisposedException_When_AddingOrQueryingDisposedScalableFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("disposed-scalable"), 1_000, 0.01);
            ScalableBloomFilter filter = new(config, context);

            // Act
            filter.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => filter.Add("scalable-key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Add("scalable-key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("scalable-key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("scalable-key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.GetPopCount());
        }

        [Fact]
        public void Should_ThrowObjectDisposedException_When_AddingOrQueryingDisposedRotatingFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("disposed-rotating"), 1_000, 0.01);
            RotatingBloomFilter filter = new(config, context, TimeSpan.FromHours(1), 2);

            // Act
            filter.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => filter.Add("rot-key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Add("rot-key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("rot-key"u8));
            Assert.Throws<ObjectDisposedException>(() => filter.Contains("rot-key".AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => filter.GetPopCount());
        }

        [Fact]
        public async Task Should_ThrowObjectDisposedException_When_PersistingOrReloadingDisposedFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration inMemConfig = this._configFactory.Create(FilterName.Parse("disposed-io-inmem"), 1_000, 0.01);
            InMemoryBloomFilter inMemFilter = new(inMemConfig, context);

            BloomFilterConfiguration rotConfig = this._configFactory.Create(FilterName.Parse("disposed-io-rot"), 1_000, 0.01);
            RotatingBloomFilter rotFilter = new(rotConfig, context, TimeSpan.FromHours(1), 2);

            BloomFilterConfiguration shardedConfig = this._configFactory.Create(FilterName.Parse("disposed-io-shard"), 2_000, 0.01).WithShardCount(2);
            ShardedBloomFilter shardedFilter = new(shardedConfig, context);

            BloomFilterConfiguration scalableConfig = this._configFactory.Create(FilterName.Parse("disposed-io-scale"), 1_000, 0.01);
            ScalableBloomFilter scalableFilter = new(scalableConfig, context);

            // Act
            inMemFilter.Dispose();
            rotFilter.Dispose();
            shardedFilter.Dispose();
            scalableFilter.Dispose();

            // Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => inMemFilter.SaveAsync(TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => inMemFilter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAsync<ObjectDisposedException>(() => rotFilter.SaveAsync(TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => rotFilter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAsync<ObjectDisposedException>(() => shardedFilter.SaveAsync(TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => shardedFilter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAsync<ObjectDisposedException>(() => scalableFilter.SaveAsync(TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => scalableFilter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class Idempotency : BloomFilterDisposalTests {
        [Fact]
        public void Should_BeIdempotent_When_DisposedMultipleTimes() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("idempotent-dispose"), 1_000, 0.01);
            using InMemoryBloomFilter inMemFilter = new(config, context);
            using RotatingBloomFilter rotFilter = new(config, context, TimeSpan.FromHours(1), 2);
            using ShardedBloomFilter shardedFilter = new(config.WithShardCount(2), context);
            using ScalableBloomFilter scalableFilter = new(config, context);

            // Act & Assert (multiple disposes must not throw)
            inMemFilter.Dispose();
            inMemFilter.Dispose();

            rotFilter.Dispose();
            rotFilter.Dispose();

            shardedFilter.Dispose();
            shardedFilter.Dispose();

            scalableFilter.Dispose();
            scalableFilter.Dispose();
        }
    }
}