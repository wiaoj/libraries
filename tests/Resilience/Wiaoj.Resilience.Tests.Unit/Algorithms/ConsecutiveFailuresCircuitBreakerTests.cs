using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;

namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "ConsecutiveFailures")]
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

    public sealed class TheTrippingLogic {
        [Fact]
        public async Task TryAcquireAsync_WhenNoFailuresOccurred_AllowsExecutionInClosedState() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 3);
            const string key = "service-endpoint-1";

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
            Assert.Null(decision.RetryAfter);
        }

        [Fact]
        public async Task TryAcquireAsync_TripsToOpen_WhenFailuresReachThresholdConsecutively() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30));
            const string key = "service-endpoint-2";

            // Attempt 1: 1. failure -> Still Closed
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d1 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d1.IsAllowed);
            Assert.Equal(CircuitState.Closed, d1.State);

            // Attempt 2: 2. failure -> Still Closed
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d2 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d2.IsAllowed);
            Assert.Equal(CircuitState.Closed, d2.State);

            // Attempt 3: 3. failure (Threshold reached) -> Trips to OPEN!
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d3 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            Assert.False(d3.IsAllowed);
            Assert.Equal(CircuitState.Open, d3.State);
            Assert.NotNull(d3.RetryAfter);
            Assert.Equal(TimeSpan.FromSeconds(30), d3.RetryAfter.Value);
        }

        [Fact]
        public async Task OnSuccessAsync_InClosedState_ResetsFailureStreak() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 3);
            const string key = "service-endpoint-3";

            // 2 failures occur
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // An intermittent success arrives -> Resets streak to 0
            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            // 2 more failures occur (Total 4, but current consecutive streak is only 2)
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // Circuit must remain CLOSED
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }

    public sealed class TheHalfOpenRecoveryFlow {
        [Fact]
        public async Task TryAcquireAsync_WhenBreakDurationExpires_AllowsProbeInHalfOpenState() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureThreshold: 1,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-endpoint-recovery";

            // Trip the circuit
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            Assert.False((await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).IsAllowed);

            // Advance time past break duration (11 seconds)
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            // Should enter Half-Open
            CircuitExecutionDecision probeDecision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probeDecision.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probeDecision.State);
        }

        [Fact]
        public async Task OnSuccessAsync_WhenInHalfOpen_ClosesCircuitAndResetsState() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureThreshold: 1,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-endpoint-success-reset";

            // 1. Trip circuit
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // 2. Advance to Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(11));
            Assert.Equal(CircuitState.HalfOpen, (await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).State);

            // 3. Probe succeeds
            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            // 4. Circuit must be CLOSED
            CircuitExecutionDecision finalDecision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(finalDecision.IsAllowed);
            Assert.Equal(CircuitState.Closed, finalDecision.State);
            Assert.Null(finalDecision.RetryAfter);
        }
    }
}