using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Tests.Unit;

public sealed class BloomFilterProviderTests {
    [Fact]
    public async Task GetAsync_Should_Return_Same_Instance_For_Same_Name() {
        // Arrange
        BloomFilterOptions options = new();
        options.Filters["test-filter"] = new FilterDefinition { ExpectedItems = 1000, ErrorRate = 0.01 };

        IOptionsMonitor<BloomFilterOptions> mockOptions = Substitute.For<IOptionsMonitor<BloomFilterOptions>>();
        mockOptions.CurrentValue.Returns(options);

        BloomFilterProvider provider = new(
            mockOptions,
            NullLogger<BloomFilterProvider>.Instance,
            NullLoggerFactory.Instance,
            Enumerable.Empty<IAutoBloomFilterSeeder>(),
            TimeProvider.System
        );

        // Act
        IPersistentBloomFilter filter1 = await provider.GetAsync("test-filter");
        IPersistentBloomFilter filter2 = await provider.GetAsync("test-filter");

        // Assert
        Assert.Same(filter1, filter2); // Caching kontrolü
    }

    [Fact]
    public async Task GetAsync_Should_Trigger_Seeder_When_Load_Fails() {
        FilterName name = "test";
        BloomFilterOptions options = new();
        options.Filters[name] = new FilterDefinition { ExpectedItems = 1000, ErrorRate = 0.01 };
        options.Lifecycle.AutoReseed = true;

        IAutoBloomFilterSeeder mockSeeder = Substitute.For<IAutoBloomFilterSeeder>();
        mockSeeder.FilterName.Returns(name);

        IBloomFilterStorage mockStorage = Substitute.For<IBloomFilterStorage>();
        mockStorage.LoadStreamAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .ThrowsAsync(new Exception("Corrupt file"));

        IOptionsMonitor<BloomFilterOptions> mockOptions = Substitute.For<IOptionsMonitor<BloomFilterOptions>>();
        mockOptions.CurrentValue.Returns(options);

        BloomFilterProvider provider = new(
            mockOptions,
            NullLogger<BloomFilterProvider>.Instance,
            NullLoggerFactory.Instance,
            new[] { mockSeeder },
            TimeProvider.System,
            mockStorage
        );

        IPersistentBloomFilter filter = await provider.GetAsync(name);

        // ÇÖZÜM: Task.Run içindeki işlemin başlaması için küçük bir gecikme ekliyoruz.
        // Normalde fire-and-forget test edilmemelidir ancak mevcut tasarımda bu gereklidir.
        await Task.Delay(50);

        await mockSeeder.Received(1).SeedAsync(
            Arg.Any<IPersistentBloomFilter>(),
            Arg.Any<CancellationToken>());
    }
}