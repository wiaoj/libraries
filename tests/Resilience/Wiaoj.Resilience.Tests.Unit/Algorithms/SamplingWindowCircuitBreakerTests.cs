using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
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

    public sealed class TheConstructorValidation {
        [Fact]
        public void GivenNullCounterFactory_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new SamplingWindowCircuitBreaker(null!, new SamplingWindowCircuitBreakerOptions()));
        }

        [Fact]
        public void GivenNullOptions_ThrowsArgumentNullException() {
            ServiceCollection services = new();
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory factory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            Assert.ThrowsAny<ArgumentNullException>(() =>
                new SamplingWindowCircuitBreaker(factory, null!));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public void GivenInvalidFailureRateThreshold_ThrowsArgumentOutOfRangeException(double invalidRate) {
            ServiceCollection services = new();
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory factory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                new SamplingWindowCircuitBreaker(factory, new SamplingWindowCircuitBreakerOptions { FailureRateThreshold = invalidRate }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenInvalidMinimumThroughput_ThrowsArgumentOutOfRangeException(int invalidThroughput) {
            ServiceCollection services = new();
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory factory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                new SamplingWindowCircuitBreaker(factory, new SamplingWindowCircuitBreakerOptions { MinimumThroughput = invalidThroughput }));
        }
    }

    public sealed class TheFailureRateCalculation {
        [Fact]
        public async Task TryAcquireAsync_DoesNotTrip_WhenVolumeIsBelowMinimumThroughput() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "service-low-volume";

            for(int i = 0; i < 5; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task TryAcquireAsync_TripsToOpen_WhenFailureRateExceedsThresholdAtMinimumVolume() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 10);
            const string key = "service-high-failure-rate";

            for(int i = 0; i < 4; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }
            for(int i = 0; i < 6; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

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

            for(int i = 0; i < 8; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            timeProvider.Advance(TimeSpan.FromSeconds(12));

            for(int i = 0; i < 3; i++) {
                await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }

    public sealed class TheHalfOpenRecoveryFlow {
        [Fact]
        public async Task TryAcquireAsync_InHalfOpen_AllowsUpToNPermittedCalls_AndDeniesExcess() {
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 1,
                permittedCallsInHalfOpen: 3,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-half-open-bounded";

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            CircuitExecutionDecision p1 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision p2 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            CircuitExecutionDecision p3 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            Assert.True(p1.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, p1.State);
            Assert.True(p2.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, p2.State);
            Assert.True(p3.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, p3.State);

            CircuitExecutionDecision p4 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(p4.IsAllowed);
            Assert.Equal(CircuitState.Open, p4.State);
            Assert.NotNull(p4.RetryAfter);
        }

        [Fact]
        public async Task OnSuccessAsync_InHalfOpen_ClosesCircuitAndResetsTripState() {
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 1,
                permittedCallsInHalfOpen: 2,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-half-open-success";

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task OnFailureAsync_InHalfOpen_ReTripsCircuitImmediately() {
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 1,
                permittedCallsInHalfOpen: 2,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-half-open-probe-fail";

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
        }
    }

    public sealed class TheKeyIsolation {
        [Fact]
        public async Task DifferentKeys_MaintainIndependentFailureRatesAndWindows() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut(failureRateThreshold: 0.5, minimumThroughput: 2);

            await breaker.OnFailureAsync("service_a", TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync("service_a", TestContext.Current.CancellationToken);

            await breaker.OnSuccessAsync("service_b", TestContext.Current.CancellationToken);
            await breaker.OnSuccessAsync("service_b", TestContext.Current.CancellationToken);

            CircuitExecutionDecision decisionA = await breaker.TryAcquireAsync("service_a", TestContext.Current.CancellationToken);
            CircuitExecutionDecision decisionB = await breaker.TryAcquireAsync("service_b", TestContext.Current.CancellationToken);

            Assert.False(decisionA.IsAllowed);
            Assert.True(decisionB.IsAllowed);
        }
    }

    public sealed class TheCancellationBehavior {
        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSut();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                breaker.TryAcquireAsync("service_cancel", cts.Token).AsTask());
        }
    }
}