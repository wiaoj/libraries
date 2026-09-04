using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterServiceTests {
    private readonly BloomFilterRegistry _registry = new();
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    private (BloomFilterService Service, InMemoryBloomFilter Filter) CreateSut(string name, long expectedItems, double errorRate) {
        BloomFilterOptions options = new();
        options.Filters[name] = new FilterDefinition { ExpectedItems = expectedItems, ErrorRate = errorRate };

        BloomFilterContext context = new(
            new FakeBloomFilterStorage(),
            new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            NullLogger.Instance,
            options,
            TimeProvider.System,
            this._configFactory
        );

        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse(name), expectedItems, errorRate);
        InMemoryBloomFilter filter = new(config, context);
        this._registry.Register(filter);

        ServiceCollection sc = new();
        sc.AddKeyedSingleton<IPersistentBloomFilter>(name, filter);
        IServiceProvider sp = sc.BuildServiceProvider();

        BloomFilterService service = new(
            sp,
            this._registry,
            Options.Create(options),
            NullLogger<BloomFilterService>.Instance,
            new FakeBloomFilterStorage()
        );

        return (service, filter);
    }

    public sealed class StatisticsMethods : BloomFilterServiceTests {
        [Fact]
        public async Task Should_CalculateDetailedStats_Accurately() {
            // Arrange
            (BloomFilterService? service, InMemoryBloomFilter? filter) = CreateSut("stats-filter", 1_000, 0.01);
            filter.Add("metric-item-1");
            filter.Add("metric-item-2");

            // Act
            BloomFilterDetailedStats detailedStats = await service.GetDetailedStatsAsync(FilterName.Parse("stats-filter"));

            // Assert
            Assert.Equal("stats-filter", detailedStats.Name);
            Assert.True(detailedStats.SetBits > 0);
            Assert.True(detailedStats.FillRatio > 0.0);
            Assert.Equal(filter.Configuration.SizeInBits, detailedStats.TotalBits);
            Assert.Equal(filter.Configuration.HashFunctionCount, detailedStats.HashFunctions);
        }

        [Fact]
        public async Task Should_ReturnAllFilterSummaries_Correctly() {
            // Arrange
            (BloomFilterService? service, InMemoryBloomFilter? filter) = CreateSut("summary-filter", 5_000, 0.05);
            filter.Add("sample");

            // Act
            IReadOnlyDictionary<FilterName, BloomFilterStats> allStats = await service.GetAllStatsAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(allStats.ContainsKey(FilterName.Parse("summary-filter")));
            BloomFilterStats stats = allStats[FilterName.Parse("summary-filter")];
            Assert.Equal("summary-filter", stats.Name);
            Assert.True(stats.IsHealthy);
        }
    }
}