using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Factory")]
[Trait("Feature", "MultiStorage")]
public sealed class DistributedCounterFactoryMultiStorageTests {

    [Fact]
    public async Task GivenDifferentTagsWithConfiguredStorages_RoutesOperationsToRespectiveStorages() {
        // Arrange
        ServiceCollection services = new();
        FakeCounterStorage defaultStorage = new();
        FakeCounterStorage customStorageA = new();
        FakeCounterStorage customStorageB = new();

        services.AddSingleton<ICounterKeyBuilder, DefaultCounterKeyBuilder>();
        services.AddSingleton<ICounterStorage>(defaultStorage);

        DistributedCounterOptions options = new() {
            DefaultStrategy = CounterStrategy.Immediate,
            GlobalKeyPrefix = "app:"
        };

        // TagA -> customStorageA
        options.AddCounter<SecurityTag>(cfg => {
            cfg.Strategy = CounterStrategy.Immediate;
            cfg.UseStorage(_ => customStorageA);
        });

        // TagB -> customStorageB
        options.AddCounter<WorkerTag>(cfg => {
            cfg.Strategy = CounterStrategy.Immediate;
            cfg.UseStorage(_ => customStorageB);
        });

        // TagDefault -> (not configured, falls back to defaultStorage)

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        DistributedCounterFactory factory = new(
            defaultStorage,
            new DefaultCounterKeyBuilder(),
            Options.Create(options),
            serviceProvider);

        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        IDistributedCounter counterA = factory.Create<SecurityTag>();
        IDistributedCounter counterB = factory.Create<WorkerTag>();
        IDistributedCounter counterDefault = factory.Create<DefaultMetricTag>();

        await counterA.IncrementAsync(10, CounterExpiry.Infinite, ct);
        await counterB.IncrementAsync(25, CounterExpiry.Infinite, ct);
        await counterDefault.IncrementAsync(5, CounterExpiry.Infinite, ct);

        // Assert: Each storage must strictly record only its targeted counter
        customStorageA.ShouldHaveValue(counterA.Key, 10);
        Assert.Equal(1, customStorageA.AtomicIncrementCallCount);
        Assert.Equal(0, customStorageA.Snapshot.Count(k => k.Key != counterA.Key.Value));

        customStorageB.ShouldHaveValue(counterB.Key, 25);
        Assert.Equal(1, customStorageB.AtomicIncrementCallCount);
        Assert.Equal(0, customStorageB.Snapshot.Count(k => k.Key != counterB.Key.Value));

        defaultStorage.ShouldHaveValue(counterDefault.Key, 5);
        Assert.Equal(1, defaultStorage.AtomicIncrementCallCount);
    }

    [Fact]
    public async Task GivenDynamicKey_InheritsParentTagStorageConfiguration() {
        // Arrange
        ServiceCollection services = new();
        FakeCounterStorage defaultStorage = new();
        FakeCounterStorage tagStorage = new();

        services.AddSingleton<ICounterKeyBuilder, DefaultCounterKeyBuilder>();
        services.AddSingleton<ICounterStorage>(defaultStorage);

        DistributedCounterOptions options = new() {
            DefaultStrategy = CounterStrategy.Immediate,
            GlobalKeyPrefix = "app:"
        };

        options.AddCounter<SecurityTag>(cfg => {
            cfg.Strategy = CounterStrategy.Immediate;
            cfg.UseStorage(_ => tagStorage);
        });

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        DistributedCounterFactory factory = new(
            defaultStorage,
            new DefaultCounterKeyBuilder(),
            Options.Create(options),
            serviceProvider);

        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        IDistributedCounter dynamicCounter = factory.Create<SecurityTag, string>("user_123");
        await dynamicCounter.IncrementAsync(7, CounterExpiry.Infinite, ct);

        // Assert: Dynamic key under SecurityTag must route to tagStorage, not defaultStorage
        tagStorage.ShouldHaveValue(dynamicCounter.Key, 7);
        Assert.Equal(1, tagStorage.AtomicIncrementCallCount);
        Assert.Equal(0, defaultStorage.AtomicIncrementCallCount);
    }

    private sealed class SecurityTag;
    private sealed class WorkerTag;
    private sealed class DefaultMetricTag;
}