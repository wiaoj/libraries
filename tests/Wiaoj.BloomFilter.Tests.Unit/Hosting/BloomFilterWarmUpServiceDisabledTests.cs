using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.BloomFilter.Tests.Unit.Fakes;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Hosting;

public class BloomFilterWarmUpServiceDisabledTests {
    public sealed class ExecuteAsyncMethod {
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