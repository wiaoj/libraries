using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Internal;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "StateTransitions")]
public sealed class CircuitBreakerStateTransitionTests {

    private static (DistributedCircuitBreakerStore Store, FakeTimeProvider TimeProvider) CreateSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        DistributedCircuitBreakerStore store = new(
            counterFactory,
            timeProvider,
            NullLogger<DistributedCircuitBreakerStore>.Instance);

        return (store, timeProvider);
    }

    [Fact]
    public async Task CanExecuteAsync_WhenNoFailuresRecorded_ReturnsAllowedInClosedState() {
        (DistributedCircuitBreakerStore store, _) = CreateSut();
        const string key = "target-endpoint-1";

        CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(CircuitState.Closed, decision.State);
        Assert.Null(decision.RetryAfter);
    }

    [Fact]
    public async Task RecordFailureAsync_WhenBelowThreshold_RemainsClosed() {
        (DistributedCircuitBreakerStore store, _) = CreateSut();
        const string key = "target-endpoint-2";
        CircuitBreakerOptions options = new() { FailureThreshold = 3, BreakDuration = TimeSpan.FromMinutes(1) };

        // 2 failures (below threshold of 3)
        await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
        await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

        CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(CircuitState.Closed, decision.State);
        Assert.Null(decision.RetryAfter);
    }

    [Fact]
    public async Task RecordFailureAsync_WhenThresholdReached_TransitionsToOpenWithCorrectRetryAfter() {
        (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
        const string key = "target-endpoint-3";
        CircuitBreakerOptions options = new() { FailureThreshold = 2, BreakDuration = TimeSpan.FromSeconds(40) };

        // 2 failures (hits threshold of 2)
        await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
        await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

        // Advance 10 seconds into the break duration
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

        Assert.False(decision.IsAllowed);
        Assert.Equal(CircuitState.Open, decision.State);
        Assert.NotNull(decision.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(30), decision.RetryAfter.Value);
    }

    [Fact]
    public async Task CanExecuteAsync_WhenBreakDurationElapses_TransitionsToHalfOpenProbe() {
        (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
        const string key = "target-endpoint-4";
        CircuitBreakerOptions options = new() { FailureThreshold = 1, BreakDuration = TimeSpan.FromSeconds(20) };

        await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

        // Advance time past the 20-second break duration
        timeProvider.Advance(TimeSpan.FromSeconds(21));

        CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(CircuitState.HalfOpen, decision.State);
        Assert.Null(decision.RetryAfter);
    }

    [Fact]
    public async Task RecordSuccessAsync_AfterTripping_ResetsAllCountersAndClosesCircuit() {
        (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
        const string key = "target-endpoint-5";
        CircuitBreakerOptions options = new() { FailureThreshold = 1, BreakDuration = TimeSpan.FromSeconds(10) };

        // Trip the circuit
        await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

        // Advance to Half-Open and succeed
        timeProvider.Advance(TimeSpan.FromSeconds(11));
        await store.RecordSuccessAsync(key, TestContext.Current.CancellationToken);

        // Verify circuit is now clean and closed
        CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(CircuitState.Closed, decision.State);
        Assert.Null(decision.RetryAfter);
    }
}