using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ConsecutiveFailuresAlgorithm")]
public sealed class ConsecutiveFailuresCircuitBreakerTests {

    private static (ConsecutiveFailuresCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSut(
        int failureThreshold = 3,
        TimeSpan? breakDuration = null) {

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        CircuitBreakerOptions options = new() {
            FailureThreshold = failureThreshold,
            BreakDuration = breakDuration ?? TimeSpan.FromSeconds(30)
        };

        ConsecutiveFailuresCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    public sealed class TheConsecutiveFailureTripping {
        [Fact]
        public async Task TryAcquireAsync_TripsToOpen_OnlyWhenThresholdIsReachedConsecutively() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 3);
            const string key = "api-service-orders";

            // 1. First failure -> Still allowed
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d1 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d1.IsAllowed);
            Assert.Equal(CircuitState.Closed, d1.State);

            // 2. Second failure -> Still allowed
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d2 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d2.IsAllowed);
            Assert.Equal(CircuitState.Closed, d2.State);

            // 3. Third failure (Reached threshold) -> Trips to Open!
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d3 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(d3.IsAllowed);
            Assert.Equal(CircuitState.Open, d3.State);
            Assert.NotNull(d3.RetryAfter);
        }

        [Fact]
        public async Task OnSuccessAsync_InClosedState_ResetsConsecutiveFailureStreak() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 3);
            const string key = "api-service-payments";

            // 2 Failures occur
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // An intermittent success arrives -> Must reset the streak to 0
            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            // 2 More failures occur (Total 4 failures, but streak is only 2) -> Should NOT trip
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }

    public sealed class TheHalfOpenRecoveryAndProbing {
        [Fact]
        public async Task TryAcquireAsync_InHalfOpen_AllowsTrialProbe_AndClosesOnSuccess() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureThreshold: 1,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "api-service-inventory";

            // Trip circuit
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            Assert.False((await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).IsAllowed);

            // Advance time past break duration
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            // Trial probe request
            CircuitExecutionDecision probeDecision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probeDecision.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probeDecision.State);

            // Probe succeeded -> Inform circuit
            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            // Circuit must now be fully CLOSED
            CircuitExecutionDecision postRecovery = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(postRecovery.IsAllowed);
            Assert.Equal(CircuitState.Closed, postRecovery.State);
        }

        [Fact]
        public async Task TryAcquireAsync_InHalfOpen_WhenProbeFails_ReTripsImmediately() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureThreshold: 1,
                breakDuration: TimeSpan.FromSeconds(15));

            const string key = "api-service-auth";

            // 1. Trip circuit
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // 2. Advance to Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(16));
            CircuitExecutionDecision probe = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, probe.State);

            // 3. Probe fails during trial!
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // 4. Must immediately re-trip to OPEN for another 15s break duration
            CircuitExecutionDecision postFail = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(postFail.IsAllowed);
            Assert.Equal(CircuitState.Open, postFail.State);
            Assert.NotNull(postFail.RetryAfter);
            Assert.True(postFail.RetryAfter.Value <= TimeSpan.FromSeconds(15));
        }
    }
}