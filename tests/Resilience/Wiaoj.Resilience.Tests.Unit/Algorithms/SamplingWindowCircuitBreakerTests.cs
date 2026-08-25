using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "SamplingWindowAlgorithm")]
public sealed class SamplingWindowCircuitBreakerTests {

    private static (SamplingWindowCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSut(
        double failureRateThreshold = 0.5, // 50% failure rate
        int minimumThroughput = 10,       // Minimum 10 requests before evaluating rate
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
            SamplingWindow = samplingWindow ?? TimeSpan.FromSeconds(20),
            BreakDuration = breakDuration ?? TimeSpan.FromSeconds(30)
        };

        SamplingWindowCircuitBreaker breaker = new(
            counterFactory,
            options,
            timeProvider,
            NullLogger<SamplingWindowCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    public sealed class TheFailureRateEvaluation {
        [Fact]
        public async Task TryAcquireAsync_DoesNotTrip_WhenThroughputIsBelowMinimumThreshold() {
            // Arrange: 50% failure rate threshold, but requires minimum 10 requests
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "api-orders-low-traffic";

            // 5 failures and 0 successes (100% failure rate, but total requests = 5 < 10 minimum)
            for(int i = 0; i < 5; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Circuit must remain CLOSED because minimum sample volume is not yet reached
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task TryAcquireAsync_TripsToOpen_WhenFailureRateExceedsThresholdAtOrAboveMinimumVolume() {
            // Arrange: Minimum 10 requests, 50% failure rate
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "api-payments-failing";

            // 4 Successes + 6 Failures = Total 10 requests (60% failure rate >= 50%)
            for(int i = 0; i < 4; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }
            for(int i = 0; i < 6; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Circuit must trip to OPEN!
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.NotNull(decision.RetryAfter);
        }

        [Fact]
        public async Task TryAcquireAsync_DoesNotTrip_WhenFailureRateIsBelowThreshold() {
            // Arrange: Minimum 10 requests, 50% failure rate
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "api-healthy-service";

            // 8 Successes + 2 Failures = Total 10 requests (20% failure rate < 50%)
            for(int i = 0; i < 8; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }
            for(int i = 0; i < 2; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task TryAcquireAsync_ResetsSampleCounts_WhenSamplingWindowExpires() {
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 10,
                samplingWindow: TimeSpan.FromSeconds(10));

            const string key = "api-window-reset";

            // 8 Failures in first window (Below minimum of 10)
            for(int i = 0; i < 8; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Advance time past 10s sampling window -> Old samples expire!
            timeProvider.Advance(TimeSpan.FromSeconds(12));

            // In the new window, 3 successes occur
            for(int i = 0; i < 3; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }
}