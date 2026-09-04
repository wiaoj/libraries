using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.BloomFilter.Tests.Unit.Fakes;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class LazyBloomFilterProxyTests {
    private readonly BloomFilterRegistry _registry = new();
    private readonly BloomFilterFactory _factory;

    public LazyBloomFilterProxyTests() {
        BloomFilterOptions options = new();
        options.Filters["lazy-filter"] = new FilterDefinition { ExpectedItems = 5_000, ErrorRate = 0.01 };

        IOptionsMonitor<BloomFilterOptions> monitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
        this._factory = new BloomFilterFactory(
            new BloomFilterConfigurationFactory(),
            monitor,
            NullLoggerFactory.Instance,
            [],
            TimeProvider.System,
            new RecyclableMemoryStreamManager(),
            new FakeBloomFilterStorage()
        );
    }

    public sealed class InitializationAndDelegation : LazyBloomFilterProxyTests {
        [Fact]
        public void Should_LazyInitializeOnFirstAccess_AndDelegateCalls() {
            // Arrange
            using LazyBloomFilterProxy proxy = new("lazy-filter", this._factory, this._registry, NullLoggerFactory.Instance);

            // Before access, inner filter is not created
            Assert.Null(proxy.GetInnerIfCreated());

            // Act: First Add triggers synchronous initialization
            bool added = proxy.Add("first-item"u8);

            // Assert
            Assert.True(added);
            Assert.NotNull(proxy.GetInnerIfCreated());
            Assert.True(proxy.Contains("first-item"u8));
            Assert.False(proxy.Contains("missing-item"u8));
            Assert.Equal("lazy-filter", proxy.Name);
            Assert.True(proxy.IsDirty);
        }

        [Fact]
        public async Task Should_EnsureInitializedAsync_WithoutBlocking() {
            // Arrange
            using LazyBloomFilterProxy proxy = new("lazy-filter", this._factory, this._registry, NullLoggerFactory.Instance);

            // Act
            await proxy.EnsureInitializedAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(proxy.GetInnerIfCreated());
            Assert.Equal("lazy-filter", proxy.Configuration.Name.Value);
        }
    }
}