using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "NegativeAndBoundary")]
public sealed class CircuitBreakerNegativeAndBoundaryTests {

    private static (ICircuitBreaker Consecutive, SamplingWindowCircuitBreaker Sampling, FakeTimeProvider TimeProvider) CreateSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider sp = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = sp.GetRequiredService<IDistributedCounterFactory>();

        ConsecutiveFailuresCircuitBreaker consecutive = new(
            counterFactory,
            new CircuitBreakerOptions { FailureThreshold = 2, BreakDuration = TimeSpan.FromSeconds(30) },
            timeProvider,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        SamplingWindowCircuitBreaker sampling = new(
            counterFactory,
            new SamplingWindowCircuitBreakerOptions {
                FailureRateThreshold = 0.5,
                MinimumThroughput = 10,
                PermittedNumberOfCallsInHalfOpenState = 2,
                SamplingWindow = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30)
            },
            timeProvider,
            NullLogger<SamplingWindowCircuitBreaker>.Instance);

        return (consecutive, sampling, timeProvider);
    }

    public sealed class TheCancellationAndTimeoutDistinction {
        [Fact]
        public async Task ExecuteAsync_WhenCallerCancels_DoesNotRecordFailure_AndCircuitRemainsClosed() {
            (ICircuitBreaker breaker, _, _) = CreateSut();
            const string key = "caller-cancel-endpoint";

            using CancellationTokenSource cts = new();
            cts.Cancel();

            for(int i = 0; i < 5; i++) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    breaker.ExecuteAsync(key, async ct => {
                        ct.ThrowIfCancellationRequested();
                        await Task.Yield();
                        return "Should not execute";
                    }, cts.Token).AsTask());
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDownstreamThrowsTimeout_RecordsFailureAndTripsCircuit() {
            (ICircuitBreaker breaker, _, _) = CreateSut();
            const string key = "downstream-timeout-endpoint";

            await Assert.ThrowsAsync<TimeoutException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new TimeoutException("Socket timed out"), TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAsync<TimeoutException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new TimeoutException("Socket timed out"), TestContext.Current.CancellationToken).AsTask());

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
        }
    }

    public sealed class TheSamplingVolumeBoundaryChecks {
        [Fact]
        public async Task SamplingWindow_AtExact9thFailure_RemainsClosed_At10thFailure_TripsOpen() {
            (_, SamplingWindowCircuitBreaker breaker, _) = CreateSut();
            const string key = "exact-volume-boundary";

            for(int i = 0; i < 9; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            CircuitExecutionDecision d9 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d9.IsAllowed);
            Assert.Equal(CircuitState.Closed, d9.State);

            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            CircuitExecutionDecision d10 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(d10.IsAllowed);
            Assert.Equal(CircuitState.Open, d10.State);
        }
    }

    public sealed class TheHalfOpenExcessGating {
        [Fact]
        public async Task SamplingWindow_InHalfOpen_AllowsExactPermittedCount_AndDeniesAllSubsequentCallers() {
            (_, SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "half-open-gating-test";

            for(int i = 0; i < 10; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }
            Assert.False((await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).IsAllowed);

            timeProvider.Advance(TimeSpan.FromSeconds(31));

            CircuitExecutionDecision probe1 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probe1.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probe1.State);

            CircuitExecutionDecision probe2 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probe2.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probe2.State);

            CircuitExecutionDecision probe3 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(probe3.IsAllowed);
            Assert.Equal(CircuitState.Open, probe3.State);
            Assert.NotNull(probe3.RetryAfter);
        }
    }

    public sealed class TheInputValidationGuards {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Breakers_ThrowArgumentException_OnInvalidKeys(string? invalidKey) {
            (ICircuitBreaker consecutive, SamplingWindowCircuitBreaker sampling, _) = CreateSut();

            await Assert.ThrowsAnyAsync<ArgumentException>(() => consecutive.TryAcquireAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => consecutive.OnSuccessAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => consecutive.OnFailureAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAnyAsync<ArgumentException>(() => sampling.TryAcquireAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sampling.OnSuccessAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sampling.OnFailureAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.1)]
        [InlineData(1.05)]
        public void SamplingWindowOptions_Validate_Throws_OnInvalidRateThreshold(double invalidRate) {
            SamplingWindowCircuitBreakerOptions options = new() { FailureRateThreshold = invalidRate };
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void SamplingWindowOptions_Validate_Throws_OnInvalidPermittedHalfOpenCalls(int invalidCalls) {
            SamplingWindowCircuitBreakerOptions options = new() { PermittedNumberOfCallsInHalfOpenState = invalidCalls };
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
        }
    }
}