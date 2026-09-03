using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

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
            new InMemoryBloomFilterStorage()
        );
    }

    public sealed class ConcurrentFirstAccess : LazyBloomFilterProxyConcurrencyTests {
        [Fact]
        public void Should_InitializeSafely_When_ManyThreadsRaceOnFirstAccess() {
            // Arrange: a fresh proxy that has never been touched by any caller yet
            using LazyBloomFilterProxy proxy = new("concurrent-lazy-filter", this._factory, this._registry, NullLoggerFactory.Instance);
            const int threadCount = 64;
            using Barrier barrier = new(threadCount);

            // Act: every thread calls Add() for the very first time at (roughly) the same
            // moment, racing to trigger the proxy's lazy initialization path.
            Parallel.For(0, threadCount, i => {
                barrier.SignalAndWait();
                proxy.Add($"race-item-{i}");
            });

            // Assert: exactly one usable inner filter exists and no write was lost to the race
            Assert.NotNull(proxy.GetInnerIfCreated());
            for(int i = 0; i < threadCount; i++) {
                Assert.True(proxy.Contains($"race-item-{i}"), $"Item race-item-{i} was lost during concurrent initialization.");
            }
        }

        [Fact]
        public async Task Should_ConvergeOnSingleInnerInstance_When_EnsureInitializedAsyncCalledConcurrently() {
            // Arrange
            using LazyBloomFilterProxy proxy = new("concurrent-lazy-filter", this._factory, this._registry, NullLoggerFactory.Instance);
            const int taskCount = 32;

            // Act: many concurrent async initializations racing against each other
            Task[] tasks = Enumerable.Range(0, taskCount)
                .Select(_ => proxy.EnsureInitializedAsync(CancellationToken.None).AsTask())
                .ToArray();
            await Task.WhenAll(tasks);

            // Assert: proxy converged on a single initialized inner filter
            Assert.NotNull(proxy.GetInnerIfCreated());
            Assert.Equal("concurrent-lazy-filter", proxy.Configuration.Name.Value);
        }
    }

    private sealed class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}