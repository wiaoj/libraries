using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.Resilience;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "SamplingWindow")]
public sealed class SamplingWindowCircuitBreakerTests {

    private static (SamplingWindowCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSut(
        double failureRateThreshold = 0.5,
        int minimumThroughput = 10,
        int permittedCallsInHalfOpen = 3,
        TimeSpan? samplingWindow = null,
        TimeSpan? breakDuration = null) {

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        SamplingWindowCircuitBreakerOptions options = new() {
            FailureRateThreshold = failureRateThreshold,
            MinimumThroughput = minimumThroughput,
            PermittedNumberOfCallsInHalfOpenState = permittedCallsInHalfOpen,
            SamplingWindow = samplingWindow ?? TimeSpan.FromSeconds(30),
            BreakDuration = breakDuration ?? TimeSpan.FromSeconds(30)
        };

        SamplingWindowCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<SamplingWindowCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    public sealed class TheFailureRateCalculation {
        [Fact]
        public async Task TryAcquireAsync_DoesNotTrip_WhenVolumeIsBelowMinimumThroughput() {
            // Arrange: 50% failure rate threshold, minimum 10 requests required
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "service-low-volume";

            // 5 failures and 0 successes (100% failure rate, but total = 5 < 10 minimum)
            for(int i = 0; i < 5; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Circuit must remain CLOSED because sampling volume is insufficient
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task TryAcquireAsync_TripsToOpen_WhenFailureRateExceedsThresholdAtMinimumVolume() {
            // Arrange: 50% failure rate threshold, minimum 10 requests
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "service-high-failure-rate";

            // 4 successes + 6 failures = 10 total requests (60% failure rate >= 50%)
            for(int i = 0; i < 4; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }
            for(int i = 0; i < 6; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Circuit must trip to OPEN
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.NotNull(decision.RetryAfter);
        }

        [Fact]
        public async Task OnFailureAsync_ResetsMetrics_WhenSamplingWindowExpires() {
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 10,
                samplingWindow: TimeSpan.FromSeconds(10));

            const string key = "service-rolling-window";

            // 8 failures in window 1 (Below 10 throughput)
            for(int i = 0; i < 8; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Advance time past 10s sampling window -> Old window expires
            timeProvider.Advance(TimeSpan.FromSeconds(12));

            // 3 successes in new window
            for(int i = 0; i < 3; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }

            // Circuit must remain CLOSED
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }

    public sealed class TheOptionCHalfOpenPermittedCalls {
        [Fact]
        public async Task TryAcquireAsync_InHalfOpen_AllowsUpToNPermittedCalls_AndDeniesExcess() {
            // Arrange: Permitted calls in half-open = 3
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 1,
                permittedCallsInHalfOpen: 3,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-half-open-bounded";

            // Trip circuit
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // Advance past break duration -> Enters Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            // First 3 concurrent probe claims must be allowed
            CircuitExecutionDecision p1 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision p2 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision p3 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            Assert.True(p1.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, p1.State);
            Assert.True(p2.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, p2.State);
            Assert.True(p3.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, p3.State);

            // 4th concurrent claim exceeds N=3 limit -> Must be DENIED to protect target!
            CircuitExecutionDecision p4 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(p4.IsAllowed);
            Assert.Equal(CircuitState.Open, p4.State);
            Assert.NotNull(p4.RetryAfter);
        }
    }
}