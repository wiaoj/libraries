using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterDisposalTests {
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
        public async Task Should_ThrowObjectDisposedException_When_PersistingOrReloadingDisposedFilter() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("disposed-io"), 1_000, 0.01);
            InMemoryBloomFilter filter = new(config, context);

            // Act
            filter.Dispose();

            // Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => filter.SaveAsync(TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => filter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class Idempotency : BloomFilterDisposalTests {
        [Fact]
        public void Should_BeIdempotent_When_DisposedMultipleTimes() {
            // Arrange
            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("idempotent-dispose"), 1_000, 0.01);
            InMemoryBloomFilter filter = new(config, context);

            // Act & Assert
            filter.Dispose();
            filter.Dispose();
            filter.Dispose();
        }
    }
}