using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.Resilience;
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

    // ────────────────────────────────────────────────────────────────────────
    // 1. CALLER CANCELLATION VS DOWNSTREAM TIMEOUT NEGATIVE TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheCancellationAndTimeoutDistinction {
        [Fact]
        public async Task ExecuteAsync_WhenCallerCancels_DoesNotRecordFailure_AndCircuitRemainsClosed() {
            (ICircuitBreaker breaker, _, _) = CreateSut();
            const string key = "caller-cancel-endpoint";

            using CancellationTokenSource cts = new();
            cts.Cancel(); // Token is already cancelled by client

            // 5 times caller cancels the operation
            for(int i = 0; i < 5; i++) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    breaker.ExecuteAsync(key, async ct => {
                        ct.ThrowIfCancellationRequested();
                        await Task.Yield();
                        return "Should not execute";
                    }, cts.Token).AsTask());
            }

            // Assert: Despite 5 cancellations, circuit MUST remain closed (FailureThreshold was 2!)
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed, "Bug: Caller cancellation was incorrectly counted as a service failure!");
            Assert.Equal(CircuitState.Closed, decision.State);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDownstreamThrowsTimeout_RecordsFailureAndTripsCircuit() {
            (ICircuitBreaker breaker, _, _) = CreateSut();
            const string key = "downstream-timeout-endpoint";

            // 2 Downstream Timeouts occur (FailureThreshold = 2)
            await Assert.ThrowsAsync<TimeoutException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new TimeoutException("Socket timed out"), TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAsync<TimeoutException>(() =>
                breaker.ExecuteAsync<string>(key, ct => throw new TimeoutException("Socket timed out"), TestContext.Current.CancellationToken).AsTask());

            // Assert: Circuit must be OPEN now!
            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. SAMPLING WINDOW MINIMUM VOLUME NEGATIVE BOUNDARY TESTS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSamplingVolumeBoundaryChecks {
        [Fact]
        public async Task SamplingWindow_AtExact9thFailure_RemainsClosed_At10thFailure_TripsOpen() {
            // Arrange: MinimumThroughput = 10, FailureRate = 50%
            (_, SamplingWindowCircuitBreaker breaker, _) = CreateSut();
            const string key = "exact-volume-boundary";

            // Act 1: 9 Failures (100% failure rate, but volume 9 < 10)
            for(int i = 0; i < 9; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }

            // Assert 1: Must be CLOSED
            CircuitExecutionDecision d9 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d9.IsAllowed, "Negative boundary failure: Circuit tripped before reaching MinimumThroughput!");
            Assert.Equal(CircuitState.Closed, d9.State);

            // Act 2: 10th Failure arrives -> Volume reaches 10
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // Assert 2: Must trip to OPEN immediately!
            CircuitExecutionDecision d10 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(d10.IsAllowed, "Positive boundary failure: Circuit failed to trip at exact MinimumThroughput volume!");
            Assert.Equal(CircuitState.Open, d10.State);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. HALF-OPEN EXCESS PROBE REJECTION (NEGATIVE GATING)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheHalfOpenExcessGating {
        [Fact]
        public async Task SamplingWindow_InHalfOpen_AllowsExactPermittedCount_AndDeniesAllSubsequentCallers() {
            // Arrange: PermittedCallsInHalfOpen = 2
            (_, SamplingWindowCircuitBreaker breaker, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "half-open-gating-test";

            // Trip to OPEN
            for(int i = 0; i < 10; i++) {
                await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
            }
            Assert.False((await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken)).IsAllowed);

            // Advance time past break duration (31s) -> Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(31));

            // Probe #1 -> Allowed (Half-Open)
            CircuitExecutionDecision probe1 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probe1.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probe1.State);

            // Probe #2 -> Allowed (Half-Open)
            CircuitExecutionDecision probe2 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probe2.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probe2.State);

            // Probe #3 (Exceeds limit of 2) -> Must be DENIED with Open state!
            CircuitExecutionDecision probe3 = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.False(probe3.IsAllowed, "Security flaw: Excess trial probe was allowed through in Half-Open state!");
            Assert.Equal(CircuitState.Open, probe3.State);
            Assert.NotNull(probe3.RetryAfter);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. INVALID INPUTS & GUARDS (NEGATIVE CONTRACT TESTS)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheInputValidationGuards {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Breakers_ThrowArgumentException_OnInvalidKeys(string? invalidKey) {
            (ICircuitBreaker consecutive, SamplingWindowCircuitBreaker sampling, _) = CreateSut();

            // Consecutive
            await Assert.ThrowsAnyAsync<ArgumentException>(() => consecutive.TryAcquireAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => consecutive.OnSuccessAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => consecutive.OnFailureAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());

            // Sampling
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