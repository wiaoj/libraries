using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter.Hosting;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Hosting;

[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "AutoFlushMultiStorage")]
public sealed class CounterAutoFlushServiceMultiStorageTests {

    [Fact]
    public async Task WhenTimerTicks_GroupsBufferedCountersByStorageAndFlushesEachBatchToCorrectStorage() {
        // Arrange
        ServiceCollection services = new();
        FakeCounterStorage defaultStorage = new();
        FakeCounterStorage storageA = new();
        FakeCounterStorage storageB = new();

        services.AddSingleton<ICounterKeyBuilder, DefaultCounterKeyBuilder>();
        services.AddSingleton<ICounterStorage>(defaultStorage);

        DistributedCounterOptions options = new() {
            DefaultStrategy = CounterStrategy.Buffered,
            AutoFlushInterval = TimeSpan.FromSeconds(5)
        };

        options.AddBufferedCounter<MetricTagA>(cfg => cfg.UseStorage(_ => storageA));
        options.AddBufferedCounter<MetricTagB>(cfg => cfg.UseStorage(_ => storageB));

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        DistributedCounterFactory factory = new(
            defaultStorage,
            new DefaultCounterKeyBuilder(),
            Options.Create(options),
            serviceProvider);

        FakeTimeProvider timeProvider = new();
        CounterAutoFlushService service = new(
            factory,
            Options.Create(options),
            timeProvider,
            NullLogger<CounterAutoFlushService>.Instance);

        CancellationToken ct = TestContext.Current.CancellationToken;

        IDistributedCounter cA = factory.Create<MetricTagA>();
        IDistributedCounter cB = factory.Create<MetricTagB>();
        IDistributedCounter cDefault = factory.Create("default_metric");

        await cA.IncrementAsync(100, CounterExpiry.Infinite, ct);
        await cB.IncrementAsync(200, CounterExpiry.Infinite, ct);
        await cDefault.IncrementAsync(50, CounterExpiry.Infinite, ct);

        using CancellationTokenSource stoppingCts = new();
        await service.StartAsync(stoppingCts.Token);

        // Act: Advance time and synchronize thread-pool worker until flush completes
        await storageA.WaitForNextFlushAsync(timeProvider, TimeSpan.FromSeconds(5), ct);

        // Assert: Each storage must receive its own batched increment without cross-contamination
        storageA.ShouldHaveValue(cA.Key, 100);
        storageA.ShouldHaveBatchFlushCount(1);

        storageB.ShouldHaveValue(cB.Key, 200);
        storageB.ShouldHaveBatchFlushCount(1);

        defaultStorage.ShouldHaveValue(cDefault.Key, 50);
        defaultStorage.ShouldHaveBatchFlushCount(1);

        await service.StopAsync(ct);
    }

    private sealed class MetricTagA;
    private sealed class MetricTagB;
}