using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;

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

    public sealed class TheConstructorValidation {
        [Fact]
        public void GivenNullCounterFactory_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new ConsecutiveFailuresCircuitBreaker(null!, new CircuitBreakerOptions()));
        }

        [Fact]
        public void GivenNullOptions_ThrowsArgumentNullException() {
            ServiceCollection services = new();
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory factory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            Assert.ThrowsAny<ArgumentNullException>(() =>
                new ConsecutiveFailuresCircuitBreaker(factory, null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenInvalidThreshold_ThrowsArgumentOutOfRangeException(int invalidThreshold) {
            ServiceCollection services = new();
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory factory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                new ConsecutiveFailuresCircuitBreaker(factory, new CircuitBreakerOptions { FailureThreshold = invalidThreshold }));
        }
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

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
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

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

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

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromSeconds(11));

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

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromSeconds(11));
            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            CircuitExecutionDecision finalDecision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(finalDecision.IsAllowed);
            Assert.Equal(CircuitState.Closed, finalDecision.State);
        }

        [Fact]
        public async Task OnFailureAsync_WhenInHalfOpen_ReTripsCircuitToOpenImmediately() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut(
                failureThreshold: 3,
                breakDuration: TimeSpan.FromSeconds(10));

            const string key = "service-endpoint-probe-fail";

            // Trip to Open
            for(int i = 0; i < 3; i++) await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // Advance to Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(11));
            Assert.Equal(CircuitState.HalfOpen, (await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).State);

            // Probe fails!
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // Must immediately re-trip to OPEN
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
        }
    }

    public sealed class TheKeyIsolation {
        [Fact]
        public async Task DifferentKeys_MaintainCompletelyIndependentCircuitStates() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut(failureThreshold: 2);

            // Key A trips
            await breaker.OnFailureAsync("key_a", TestContext.Current.CancellationToken);
            await breaker.OnFailureAsync("key_a", TestContext.Current.CancellationToken);

            // Key B is completely healthy
            CircuitExecutionDecision decisionA = await breaker.TryAcquireAsync("key_a", TestContext.Current.CancellationToken);
            CircuitExecutionDecision decisionB = await breaker.TryAcquireAsync("key_b", TestContext.Current.CancellationToken);

            Assert.False(decisionA.IsAllowed);
            Assert.True(decisionB.IsAllowed);
        }
    }

    public sealed class TheCancellationBehavior {
        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateSut();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                breaker.TryAcquireAsync("key_cancel", cts.Token).AsTask());
        }
    }
}