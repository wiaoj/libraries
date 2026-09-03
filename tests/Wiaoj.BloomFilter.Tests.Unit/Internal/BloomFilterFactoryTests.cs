using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.Internal;

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

    public sealed class CreateMethod {
        [Fact]
        public async Task Should_CreateAndInitializeFilter_When_ConfigurationExists() {
            // Arrange
            BloomFilterOptions options = new();
            options.Filters["test-filter"] = new FilterDefinition { ExpectedItems = 1_000, ErrorRate = 0.01 };

            IOptionsMonitor<BloomFilterOptions> optionsMonitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
            BloomFilterFactory factory = new(
                new BloomFilterConfigurationFactory(),
                optionsMonitor,
                NullLoggerFactory.Instance,
                [],
                TimeProvider.System,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                new InMemoryBloomFilterStorage()
            );

            // Act
            IPersistentBloomFilter filter = await factory.Create("test-filter");

            // Assert
            Assert.NotNull(filter);
            Assert.Equal("test-filter", filter.Name);
        }

        [Fact]
        public async Task Should_ThrowInvalidOperationException_When_ConfigurationIsMissing() {
            // Arrange
            BloomFilterOptions options = new();
            IOptionsMonitor<BloomFilterOptions> optionsMonitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
            BloomFilterFactory factory = new(
                new BloomFilterConfigurationFactory(),
                optionsMonitor,
                NullLoggerFactory.Instance,
                [],
                TimeProvider.System,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream())
            );

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => factory.Create("non-existent-filter"));
        }

        [Fact]
        public async Task Should_TriggerAutoSeeder_When_DataIsCorruptOrEmpty() {
            // Arrange
            FilterName filterName = FilterName.Parse("corrupt-filter");
            BloomFilterOptions options = new();
            options.Filters[filterName.Value] = new FilterDefinition { ExpectedItems = 1_000, ErrorRate = 0.01 };
            options.Lifecycle.AutoReseed = true;

            TestAutoSeeder seeder = new(filterName);
            InMemoryBloomFilterStorage storage = new();

            // Seed a corrupted header into storage to force reload failure
            BloomFilterConfiguration config = new BloomFilterConfigurationFactory().Create(filterName, 1_000, 0.01);
            using MemoryStream corruptedStream = new([0xFF, 0xFF, 0xFF, 0xFF]);
            await storage.SaveAsync(filterName.Value, config, corruptedStream);

            IOptionsMonitor<BloomFilterOptions> optionsMonitor = new FakeOptionsMonitor<BloomFilterOptions>(options);
            BloomFilterFactory factory = new(
                new BloomFilterConfigurationFactory(),
                optionsMonitor,
                NullLoggerFactory.Instance,
                [seeder],
                TimeProvider.System,
                new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                storage
            );

            // Act
            IPersistentBloomFilter filter = await factory.Create(filterName.Value);

            // Allow background Task.Run seeder to execute
            await Task.Delay(100);

            // Assert
            Assert.True(seeder.InvocationCount >= 1);
        }
    }

    private sealed class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}