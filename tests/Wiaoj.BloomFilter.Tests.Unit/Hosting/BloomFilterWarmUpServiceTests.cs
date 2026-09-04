using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.BloomFilter.Tests.Unit.Fakes;
using Wiaoj.ObjectPool.Testing;

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
                new FakeBloomFilterStorage()
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

            // Act: Start service and wait for its internal execution task to complete
            await warmUpService.StartAsync(CancellationToken.None);
            if(warmUpService.ExecuteTask != null) {
                await warmUpService.ExecuteTask;
            }

            // Assert: Both proxies are now fully loaded in memory
            Assert.NotNull(proxy1.GetInnerIfCreated());
            Assert.NotNull(proxy2.GetInnerIfCreated());
        }

        [Fact]
        public async Task Should_NotTouchAnyFilter_When_WarmUpIsDisabled() {
            // Arrange
            ServiceCollection services = new();
            BloomFilterOptions options = new();
            options.Lifecycle.EnableWarmUp = false;
            options.Filters["cold-1"] = new FilterDefinition { ExpectedItems = 1_000, ErrorRate = 0.01 };

            IOptionsMonitor<BloomFilterOptions> monitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
            BloomFilterRegistry registry = new();
            BloomFilterFactory factory = new(
                new BloomFilterConfigurationFactory(),
                monitor,
                NullLoggerFactory.Instance,
                [],
                TimeProvider.System,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                new FakeBloomFilterStorage()
            );

            LazyBloomFilterProxy proxy = new("cold-1", factory, registry, NullLoggerFactory.Instance);
            services.AddKeyedSingleton<IPersistentBloomFilter>("cold-1", proxy);

            IServiceProvider sp = services.BuildServiceProvider();
            IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);

            using BloomFilterWarmUpService warmUpService = new(
                sp,
                optionsWrapper,
                NullLogger<BloomFilterWarmUpService>.Instance
            );

            // Act
            await warmUpService.StartAsync(CancellationToken.None);

            // Assert: filter remains lazily uninitialized since warm-up was turned off
            Assert.Null(proxy.GetInnerIfCreated());
        }
    }
}