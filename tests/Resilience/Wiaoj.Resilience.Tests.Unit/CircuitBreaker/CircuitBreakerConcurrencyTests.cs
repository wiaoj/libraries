using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Internal;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "Concurrency")]
public sealed class CircuitBreakerConcurrencyTests {

    private static DistributedCircuitBreakerStore CreateStore() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        return new DistributedCircuitBreakerStore(
            counterFactory,
            timeProvider,
            NullLogger<DistributedCircuitBreakerStore>.Instance);
    }

    [Fact]
    public async Task RecordFailureAsync_UnderParallel50Failures_TripsCircuitConsistently() {
        DistributedCircuitBreakerStore store = CreateStore();
        const string key = "concurrent-failing-endpoint";
        CircuitBreakerOptions options = new() { FailureThreshold = 5, BreakDuration = TimeSpan.FromMinutes(1) };

        // Act: 50 concurrent failures reported simultaneously
        Task[] tasks = Enumerable.Range(0, 50).Select(_ =>
            store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken).AsTask()
        ).ToArray();

        await Task.WhenAll(tasks);

        // Assert: Circuit must be OPEN
        CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
        Assert.False(decision.IsAllowed);
        Assert.Equal(CircuitState.Open, decision.State);
    }

    [Fact]
    public async Task IndependentKeys_UnderConcurrentTraffic_MaintainIsolatedState() {
        DistributedCircuitBreakerStore store = CreateStore();
        CircuitBreakerOptions options = new() { FailureThreshold = 2, BreakDuration = TimeSpan.FromMinutes(1) };

        string[] failingKeys = ["service-a", "service-b"];
        string[] healthyKeys = ["service-c", "service-d"];

        // Act: Trip failing keys in parallel, keep healthy keys untouched
        Task[] tripTasks = failingKeys.SelectMany(k => new[] {
            store.RecordFailureAsync(k, options, TestContext.Current.CancellationToken).AsTask(),
            store.RecordFailureAsync(k, options, TestContext.Current.CancellationToken).AsTask()
        }).ToArray();

        await Task.WhenAll(tripTasks);

        // Assert: Failing keys must be Open
        foreach(string failKey in failingKeys) {
            CircuitExecutionDecision dec = await store.CanExecuteAsync(failKey, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, dec.State);
            Assert.False(dec.IsAllowed);
        }

        // Assert: Healthy keys must be Closed
        foreach(string okKey in healthyKeys) {
            CircuitExecutionDecision dec = await store.CanExecuteAsync(okKey, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Closed, dec.State);
            Assert.True(dec.IsAllowed);
        }
    }
}