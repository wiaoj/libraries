using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Hosting;

public class BloomFilterSeedingServiceTests : IDisposable {
    private readonly BloomFilterRegistry _registry = new();
    private readonly FakeBloomFilterStorage _storage = new();
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public BloomFilterSeedingServiceTests() {
        BloomFilterSeedingService.ResetSeededState();
    }

    public void Dispose() {
        BloomFilterSeedingService.ResetSeededState();
        GC.SuppressFinalize(this);
    }

    private InMemoryBloomFilter CreateFilter(string name) {
        BloomFilterOptions options = new();
        BloomFilterContext context = new(
            this._storage,
            new RecyclableMemoryStreamManager(),
            NullLogger.Instance,
            options,
            TimeProvider.System,
            this._configFactory
        );

        BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse(name), 1_000, 0.01);
        InMemoryBloomFilter filter = new(config, context);
        this._registry.Register(filter);
        return filter;
    }

    private sealed class TrackingSeeder(FilterName filterName, Action? onSeed = null) : IAutoBloomFilterSeeder {
        public FilterName FilterName => filterName;
        public int InvocationCount { get; private set; }

        public Task SeedAsync(IPersistentBloomFilter filter, CancellationToken cancellationToken) {
            this.InvocationCount++;
            onSeed?.Invoke();
            return Task.CompletedTask;
        }
    }

    public sealed class ExecuteAsyncMethod : BloomFilterSeedingServiceTests {
        [Fact]
        public async Task Should_SeedEmptyFilter_When_NotYetSeeded() {
            // Arrange
            using InMemoryBloomFilter filter = CreateFilter("seed-new-filter");
            TrackingSeeder seeder = new(filter.Name, () => filter.Add("seeded-item"u8));

            using BloomFilterSeedingService service = new(
                this._registry,
                [seeder],
                NullLogger<BloomFilterSeedingService>.Instance,
                this._storage
            );

            // Act
            await service.StartAsync(TestContext.Current.CancellationToken);
            if(service.ExecuteTask != null) {
                await service.ExecuteTask;
            }

            // Assert
            Assert.Equal(1, seeder.InvocationCount);
            Assert.True(filter.Contains("seeded-item"u8));
        }

        [Fact]
        public async Task Should_NotReinvokeSeeder_When_FilterWasAlreadySeededWithZeroItems() {
            // Arrange: Source database table is empty, so seeder inserts 0 items
            using InMemoryBloomFilter filter = CreateFilter("empty-db-table-filter");
            TrackingSeeder seeder = new(filter.Name); // Inserts 0 items, PopCount stays 0

            // Act 1: Initial startup
            using(BloomFilterSeedingService service1 = new(
                this._registry,
                [seeder],
                NullLogger<BloomFilterSeedingService>.Instance,
                this._storage)) {

                await service1.StartAsync(TestContext.Current.CancellationToken);
                if(service1.ExecuteTask != null) {
                    await service1.ExecuteTask;
                }
            }

            Assert.Equal(1, seeder.InvocationCount);
            Assert.Equal(0, filter.GetPopCount());

            // Act 2: Subsequent startup / cycle
            using(BloomFilterSeedingService service2 = new(
                this._registry,
                [seeder],
                NullLogger<BloomFilterSeedingService>.Instance,
                this._storage)) {

                await service2.StartAsync(TestContext.Current.CancellationToken);
                if(service2.ExecuteTask != null) {
                    await service2.ExecuteTask;
                }
            }

            // Assert: Must not re-invoke seeder even though PopCount is 0
            Assert.Equal(1, seeder.InvocationCount);
        }

        [Fact]
        public async Task Should_SkipSeeding_When_StorageAlreadyContainsPersistedSnapshot() {
            // Arrange: Pre-populate snapshot on storage (representing prior persistence)
            using InMemoryBloomFilter filter = CreateFilter("pre-persisted-filter");
            filter.Add("dummy"u8);
            await filter.SaveAsync(TestContext.Current.CancellationToken);

            // Reset in-memory seeded set to simulate a fresh application restart
            BloomFilterSeedingService.ResetSeededState();

            TrackingSeeder seeder = new(filter.Name);

            // Act
            using BloomFilterSeedingService service = new(
                this._registry,
                [seeder],
                NullLogger<BloomFilterSeedingService>.Instance,
                this._storage
            );

            await service.StartAsync(TestContext.Current.CancellationToken);
            if(service.ExecuteTask != null) {
                await service.ExecuteTask;
            }

            // Assert: Seeder is skipped because persistent storage already has a valid snapshot
            Assert.Equal(0, seeder.InvocationCount);
        }
    }
}
