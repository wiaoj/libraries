using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.BloomFilter.Tests.Unit.Fakes;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class LazyBloomFilterProxyConcurrencyTests {
    private readonly BloomFilterRegistry _registry = new();
    private readonly BloomFilterFactory _factory;

    public LazyBloomFilterProxyConcurrencyTests() {
        BloomFilterOptions options = new();
        options.Filters["concurrent-lazy-filter"] = new FilterDefinition { ExpectedItems = 5_000, ErrorRate = 0.01 };

        IOptionsMonitor<BloomFilterOptions> monitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
        this._factory = new BloomFilterFactory(
            new BloomFilterConfigurationFactory(),
            monitor,
            NullLoggerFactory.Instance,
            [],
            TimeProvider.System,
            new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            new FakeBloomFilterStorage()
        );
    }

    public sealed class ConcurrentFirstAccess : LazyBloomFilterProxyConcurrencyTests {
        [Fact]
        public async Task Should_InitializeSafely_When_ManyThreadsRaceOnFirstAccess() {
            // Arrange
            using LazyBloomFilterProxy proxy = new("concurrent-lazy-filter", this._factory, this._registry, NullLoggerFactory.Instance);
            const int taskCount = 64;

            // Act: Race 64 concurrent tasks to trigger initialization without starving the threadpool
            Task[] tasks = [.. Enumerable.Range(0, taskCount).Select(i => Task.Run(() => proxy.Add($"race-item-{i}")))];

            await Task.WhenAll(tasks);

            // Assert: Inner filter must be initialized and all items must be found
            Assert.NotNull(proxy.GetInnerIfCreated());
            for(int i = 0; i < taskCount; i++) {
                Assert.True(proxy.Contains($"race-item-{i}"), $"Item race-item-{i} was lost during concurrent initialization.");
            }
        }

        [Fact]
        public async Task Should_ConvergeOnSingleInnerInstance_When_EnsureInitializedAsyncCalledConcurrently() {
            // Arrange
            using LazyBloomFilterProxy proxy = new("concurrent-lazy-filter", this._factory, this._registry, NullLoggerFactory.Instance);
            const int taskCount = 32;

            // Act
            Task[] tasks = [.. Enumerable.Range(0, taskCount).Select(_ => proxy.EnsureInitializedAsync(TestContext.Current.CancellationToken).AsTask())];

            await Task.WhenAll(tasks);

            // Assert
            Assert.NotNull(proxy.GetInnerIfCreated());
            Assert.Equal("concurrent-lazy-filter", proxy.Configuration.Name);
        }
    }
}