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

        [Fact]
        public async Task Should_ReportUnhealthy_When_FilterIsOverloadedBeyondTheoreticalLimit() {
            // Arrange: tiny filter (10 items, 0.01 error rate) overloaded with 5,000 items
            (BloomFilterService? service, InMemoryBloomFilter? filter) = CreateSut("overloaded-filter", 10, 0.01);
            for(int i = 0; i < 5_000; i++) {
                filter.Add($"overload-key-{i}");
            }

            // Act
            IReadOnlyDictionary<FilterName, BloomFilterStats> allStats = await service.GetAllStatsAsync(TestContext.Current.CancellationToken);

            // Assert
            BloomFilterStats stats = allStats[FilterName.Parse("overloaded-filter")];
            Assert.False(stats.IsHealthy);
        }

        [Fact]
        public async Task Should_CheckHealthBasedOnSaturationThreshold_ForScalableFilter() {
            // Arrange
            BloomFilterOptions options = new();
            options.Filters["scalable-filter"] = new FilterDefinition {
                ExpectedItems = 100,
                ErrorRate = 0.01,
                Type = BloomFilterType.Scalable,
                SaturationThreshold = 0.40
            };

            BloomFilterContext context = new(
                new FakeBloomFilterStorage(),
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                NullLogger.Instance,
                options,
                TimeProvider.System,
                this._configFactory
            );

            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("scalable-filter"), 100, 0.01);
            InMemoryBloomFilter filter = new(config, context);

            ServiceCollection sc = new();
            sc.AddKeyedSingleton<IPersistentBloomFilter>("scalable-filter", filter);
            IServiceProvider sp = sc.BuildServiceProvider();

            BloomFilterService service = new(
                sp,
                this._registry,
                Options.Create(options),
                NullLogger<BloomFilterService>.Instance
            );

            // Act 1: Empty filter should be healthy
            IReadOnlyDictionary<FilterName, BloomFilterStats> stats1 = await service.GetAllStatsAsync(TestContext.Current.CancellationToken);
            Assert.True(stats1[FilterName.Parse("scalable-filter")].IsHealthy);

            // Act 2: Overfill bits beyond 40% threshold
            for(int i = 0; i < 500; i++) {
                filter.Add($"overfill-{i}");
            }

            IReadOnlyDictionary<FilterName, BloomFilterStats> stats2 = await service.GetAllStatsAsync(TestContext.Current.CancellationToken);
            Assert.False(stats2[FilterName.Parse("scalable-filter")].IsHealthy);
        }
    }

    public sealed class ManagementMethods : BloomFilterServiceTests {
        [Fact]
        public async Task Should_SaveOnlyDirtyFilters_When_SaveAllAsyncCalled() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterOptions options = new();
            options.Filters["dirty-filter"] = new FilterDefinition { ExpectedItems = 100, ErrorRate = 0.01 };
            options.Filters["clean-filter"] = new FilterDefinition { ExpectedItems = 100, ErrorRate = 0.01 };

            BloomFilterContext context = new(
                storage,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                NullLogger.Instance,
                options,
                TimeProvider.System,
                this._configFactory
            );

            InMemoryBloomFilter dirtyFilter = new(this._configFactory.Create(FilterName.Parse("dirty-filter"), 100, 0.01), context);
            InMemoryBloomFilter cleanFilter = new(this._configFactory.Create(FilterName.Parse("clean-filter"), 100, 0.01), context);

            dirtyFilter.Add("something"u8);
            Assert.True(dirtyFilter.IsDirty);
            Assert.False(cleanFilter.IsDirty);

            this._registry.Register(dirtyFilter);
            this._registry.Register(cleanFilter);

            ServiceCollection sc = new();
            sc.AddKeyedSingleton<IPersistentBloomFilter>("dirty-filter", dirtyFilter);
            sc.AddKeyedSingleton<IPersistentBloomFilter>("clean-filter", cleanFilter);
            IServiceProvider sp = sc.BuildServiceProvider();

            BloomFilterService service = new(
                sp,
                this._registry,
                Options.Create(options),
                NullLogger<BloomFilterService>.Instance,
                storage
            );

            // Act
            await service.SaveAllAsync(TestContext.Current.CancellationToken);

            // Assert: dirty filter was flushed, clean filter remained clean
            Assert.False(dirtyFilter.IsDirty);
            Assert.False(cleanFilter.IsDirty);
            Assert.True(storage.Exists("dirty-filter"));
            Assert.False(storage.Exists("clean-filter"));
        }

        [Fact]
        public async Task Should_ReloadSpecificFilter_When_ReloadFilterAsyncCalled() {
            // Arrange
            (BloomFilterService service, InMemoryBloomFilter filter) = CreateSut("reload-target", 1_000, 0.01);
            filter.Add("initial-item");
            await filter.SaveAsync(TestContext.Current.CancellationToken);

            // Act
            await service.ReloadFilterAsync(FilterName.Parse("reload-target"), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(filter.Contains("initial-item"));
        }

        [Fact]
        public async Task Should_DeleteFilterFromStorage_When_DeleteFilterAsyncCalled() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterOptions options = new();
            options.Filters["delete-target"] = new FilterDefinition { ExpectedItems = 100, ErrorRate = 0.01 };

            BloomFilterContext context = new(
                storage,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                NullLogger.Instance,
                options,
                TimeProvider.System,
                this._configFactory
            );

            InMemoryBloomFilter filter = new(this._configFactory.Create(FilterName.Parse("delete-target"), 100, 0.01), context);
            filter.Add("test-val");
            await filter.SaveAsync(TestContext.Current.CancellationToken);
            Assert.True(storage.Exists("delete-target"));

            ServiceCollection sc = new();
            sc.AddKeyedSingleton<IPersistentBloomFilter>("delete-target", filter);
            IServiceProvider sp = sc.BuildServiceProvider();

            BloomFilterService service = new(
                sp,
                this._registry,
                Options.Create(options),
                NullLogger<BloomFilterService>.Instance,
                storage
            );

            // Act
            await service.DeleteFilterAsync(FilterName.Parse("delete-target"), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(storage.Exists("delete-target"));
        }

        [Fact]
        public async Task Should_ThrowInvalidOperationException_When_OperatingOnUnregisteredFilter() {
            // Arrange
            (BloomFilterService service, _) = CreateSut("known-filter", 1_000, 0.01);

            // Act & Assert
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                service.GetDetailedStatsAsync(FilterName.Parse("unknown-filter")).AsTask());
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                service.ReloadFilterAsync(FilterName.Parse("unknown-filter"), TestContext.Current.CancellationToken).AsTask());
        }
    }
}