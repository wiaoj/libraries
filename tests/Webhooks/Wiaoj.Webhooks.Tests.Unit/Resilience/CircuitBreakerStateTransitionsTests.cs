using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.CircuitBreaker;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "StateTransitions")]
public sealed class CircuitBreakerStateTransitionsTests {

    private static (DistributedCircuitBreakerStore Store, FakeTimeProvider TimeProvider) CreateSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        DistributedCircuitBreakerStore store = new(counterFactory, timeProvider, NullLogger<DistributedCircuitBreakerStore>.Instance);
        return (store, timeProvider);
    }

    public sealed class TheFullLifecycleCycles {
        [Fact]
        public async Task Lifecycle_ExecutesMultipleConsecutiveFailureAndRecoveryCycles() {
            (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "endpoint-multi-cycle";
            CircuitBreakerOptions options = new() { FailureThreshold = 2, BreakDuration = TimeSpan.FromSeconds(10) };

            // ── CYCLE 1: Trip to Open -> Expire -> Half-Open Probe Fails -> Re-Trip to Open ──
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            CircuitExecutionDecision d1 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, d1.State);

            // Advance time past break duration (11s) -> Enters Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(11));
            CircuitExecutionDecision d2 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, d2.State);

            // Probe fails during trial -> Must immediately re-trip to Open for another 10s!
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d3 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, d3.State);

            // ── CYCLE 2: Expire again -> Half-Open Probe Succeeds -> Closes Circuit ──
            timeProvider.Advance(TimeSpan.FromSeconds(11));
            CircuitExecutionDecision d4 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, d4.State);

            // Probe succeeds -> Resets and closes circuit
            await store.RecordSuccessAsync(key, TestContext.Current.CancellationToken);

            CircuitExecutionDecision d5 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Closed, d5.State);
            Assert.True(d5.IsAllowed);
            Assert.Null(d5.RetryAfter);
        }

        [Fact]
        public async Task CanExecuteAsync_EvaluatesExactMillisecondBoundaryConditions() {
            (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "endpoint-micro-boundary";
            CircuitBreakerOptions options = new() { FailureThreshold = 1, BreakDuration = TimeSpan.FromSeconds(5) };

            // Trip circuit
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // 4999ms elapsed -> Still OPEN (1ms left)
            timeProvider.Advance(TimeSpan.FromMilliseconds(4999));
            CircuitExecutionDecision beforeExpiry = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, beforeExpiry.State);
            Assert.False(beforeExpiry.IsAllowed);

            // 5000ms elapsed -> Exact boundary -> HALF-OPEN
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            CircuitExecutionDecision exactBoundary = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, exactBoundary.State);
            Assert.True(exactBoundary.IsAllowed);
        }
    }

    public sealed class TheEndpointKeyIsolation {
        [Fact]
        public async Task StateTransitions_OnOneEndpoint_NeverAffectsIndependentEndpoints() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            const string healthyKey = "endpoint-healthy";
            const string failingKey = "endpoint-failing";
            CircuitBreakerOptions options = new() { FailureThreshold = 2, BreakDuration = TimeSpan.FromMinutes(1) };

            // Trip the failing key to OPEN
            await store.RecordFailureAsync(failingKey, options, TestContext.Current.CancellationToken);
            await store.RecordFailureAsync(failingKey, options, TestContext.Current.CancellationToken);

            // Check failing key -> Open
            CircuitExecutionDecision failingDecision = await store.CanExecuteAsync(failingKey, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Open, failingDecision.State);

            // Check healthy key -> Must remain completely unaffected (Closed)
            CircuitExecutionDecision healthyDecision = await store.CanExecuteAsync(healthyKey, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.Closed, healthyDecision.State);
            Assert.True(healthyDecision.IsAllowed);
        }
    }
}