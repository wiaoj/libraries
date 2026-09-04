using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Service")]
[Trait("Feature", "BatchMultiStorage")]
public sealed class DistributedCounterServiceMultiStorageTests {

    [Fact]
    public async Task GetValuesAsync_AcrossMixedStorages_FetchesCorrectValuesFromRespectiveBackends() {
        // Arrange
        ServiceCollection services = new();
        FakeCounterStorage defaultStorage = new();
        FakeCounterStorage redisFake = new();
        FakeCounterStorage inMemoryFake = new();

        DistributedCounterOptions options = new() { GlobalKeyPrefix = "app:" };

        options.AddImmediateCounter("redis_metric", cfg => cfg.UseStorage(_ => redisFake));
        options.AddImmediateCounter("memory_metric", cfg => cfg.UseStorage(_ => inMemoryFake));

        DefaultCounterKeyBuilder keyBuilder = new();

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        DistributedCounterFactory factory = new(
            defaultStorage,
            keyBuilder,
            Options.Create(options),
            serviceProvider);

        DistributedCounterService service = new(
            defaultStorage,
            keyBuilder,
            factory,
            Options.Create(options),
            serviceProvider);

        CancellationToken ct = TestContext.Current.CancellationToken;

        // Pre-seed individual storages
        redisFake.SetupGetValue(keyBuilder.Build("redis_metric", options), new CounterValue(888));
        inMemoryFake.SetupGetValue(keyBuilder.Build("memory_metric", options), new CounterValue(444));
        defaultStorage.SetupGetValue(keyBuilder.Build("default_metric", options), new CounterValue(111));

        // Act
        CounterValueCollection results = await service.GetValuesAsync(
            ["redis_metric", "memory_metric", "default_metric"],
            ct);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(888, results["redis_metric"].Value);
        Assert.Equal(444, results["memory_metric"].Value);
        Assert.Equal(111, results["default_metric"].Value);
    }
}