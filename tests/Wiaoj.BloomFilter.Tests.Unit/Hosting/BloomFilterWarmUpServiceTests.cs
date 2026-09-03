using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Hosting;

public class BloomFilterWarmUpServiceTests {
    public sealed class ExecuteAsyncMethod {
        [Fact]
        public async Task Should_WarmUpAllRegisteredFilters_When_Enabled() {
            // Arrange
            ServiceCollection services = new();
            BloomFilterOptions options = new();
            options.Lifecycle.EnableWarmUp = true;
            options.Filters["warm-1"] = new FilterDefinition { ExpectedItems = 1_000, ErrorRate = 0.01 };
            options.Filters["warm-2"] = new FilterDefinition { ExpectedItems = 2_000, ErrorRate = 0.01 };

            IOptionsMonitor<BloomFilterOptions> monitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
            BloomFilterRegistry registry = new();
            BloomFilterFactory factory = new(
                new BloomFilterConfigurationFactory(),
                monitor,
                NullLoggerFactory.Instance,
                [],
                TimeProvider.System,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                new InMemoryBloomFilterStorage()
            );

            LazyBloomFilterProxy proxy1 = new("warm-1", factory, registry, NullLoggerFactory.Instance);
            LazyBloomFilterProxy proxy2 = new("warm-2", factory, registry, NullLoggerFactory.Instance);

            services.AddKeyedSingleton<IPersistentBloomFilter>("warm-1", proxy1);
            services.AddKeyedSingleton<IPersistentBloomFilter>("warm-2", proxy2);

            IServiceProvider sp = services.BuildServiceProvider();
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            using BloomFilterWarmUpService warmUpService = new(
                sp,
                optionsWrapper,
                NullLogger<BloomFilterWarmUpService>.Instance
            );

            // Act
            await warmUpService.StartAsync(CancellationToken.None);

            // Assert: Both proxies should now be loaded into memory
            Assert.NotNull(proxy1.GetInnerIfCreated());
            Assert.NotNull(proxy2.GetInnerIfCreated());
        }
    }

    private sealed class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}