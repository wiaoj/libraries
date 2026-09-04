using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.BloomFilter.Tests.Unit.Fakes;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterFactoryTests {
    private sealed class TestAutoSeeder(FilterName filterName) : IAutoBloomFilterSeeder {
        public FilterName FilterName { get; } = filterName;
        public int InvocationCount { get; private set; }

        public Task SeedAsync(IPersistentBloomFilter filter, CancellationToken cancellationToken) {
            this.InvocationCount++;
            filter.Add("seeded-item"u8);
            return Task.CompletedTask;
        }
    }

    private static BloomFilterFactory CreateFactory(
        BloomFilterOptions options,
        IBloomFilterStorage? storage = null,
        IEnumerable<IAutoBloomFilterSeeder>? seeders = null,
        IHostApplicationLifetime? hostLifetime = null) {

        return new BloomFilterFactory(
            new BloomFilterConfigurationFactory(),
            new FakeOptionsMonitor<BloomFilterOptions>(options),
            NullLoggerFactory.Instance,
            seeders ?? [],
            TimeProvider.System,
            new RecyclableMemoryStreamManager(),
            storage ?? new FakeBloomFilterStorage()
        );
    }

    public sealed class CreateMethod {
        [Fact]
        public async Task Should_CreateAndInitializeFilter_When_ConfigurationExists() {
            // Arrange
            BloomFilterOptions options = new();
            options.Filters["test-filter"] = new FilterDefinition { ExpectedItems = 1_000, ErrorRate = 0.01 };

            BloomFilterFactory factory = CreateFactory(options);

            // Act
            IPersistentBloomFilter filter = await factory.Create("test-filter", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(filter);
            Assert.Equal("test-filter", filter.Name);
        }

        [Fact]
        public async Task Should_ThrowInvalidOperationException_When_ConfigurationIsMissing() {
            // Arrange
            BloomFilterOptions options = new();
            BloomFilterFactory factory = CreateFactory(options);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => factory.Create("non-existent-filter", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_ThrowValidationException_When_FilterConfigurationIsInvalid() {
            // Arrange
            BloomFilterOptions options = new();
            options.Filters["bad-filter"] = new FilterDefinition {
                ExpectedItems = 10_000,
                ErrorRate = 0.01,
                Type = BloomFilterType.Sharded,
                ShardCount = 3 // Invalid non-power of 2
            };
            BloomFilterFactory factory = CreateFactory(options);

            // Act & Assert: Factory must validate the definition before creating
            await Assert.ThrowsAnyAsync<ArgumentException>(() => factory.Create("bad-filter", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_TriggerAutoSeeder_When_DataIsCorruptOrEmpty() {
            // Arrange
            FilterName filterName = FilterName.Parse("corrupt-filter");
            BloomFilterOptions options = new();
            options.Filters[filterName.Value] = new FilterDefinition { ExpectedItems = 1_000, ErrorRate = 0.01 };
            options.Lifecycle.AutoReseed = true;

            TestAutoSeeder seeder = new(filterName);
            FakeBloomFilterStorage storage = new();

            // Seed a corrupted header into storage to force reload failure
            BloomFilterConfiguration config = new BloomFilterConfigurationFactory().Create(filterName, 1_000, 0.01);
            using MemoryStream corruptedStream = new([0xFF, 0xFF, 0xFF, 0xFF]);
            await storage.SaveAsync(filterName.Value, config, corruptedStream, TestContext.Current.CancellationToken);

            BloomFilterFactory factory = CreateFactory(options, storage: storage, seeders: [seeder]);

            // Act
            IPersistentBloomFilter filter = await factory.Create(filterName.Value, TestContext.Current.CancellationToken);

            // Allow background Task.Run seeder to execute
            await Task.Delay(100, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(seeder.InvocationCount >= 1);
        }
    }
}