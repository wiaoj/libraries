using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "CircuitStateInspection")]
public sealed class CircuitBreakerStateInspectionTests {

    private static IDistributedCounterFactory CreateCounterFactory(TimeProvider timeProvider) {
        ServiceCollection services = new();
        services.AddSingleton(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());
        return services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();
    }

    private static (ConsecutiveFailuresCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateConsecutiveSut(
        int failureThreshold = 3,
        TimeSpan? breakDuration = null) {

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        CircuitBreakerOptions options = new() {
            FailureThreshold = failureThreshold,
            BreakDuration = breakDuration ?? TimeSpan.FromSeconds(30)
        };

        ConsecutiveFailuresCircuitBreaker breaker = new(
            CreateCounterFactory(timeProvider),
            options,
            timeProvider,
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    private static (SamplingWindowCircuitBreaker Breaker, FakeTimeProvider TimeProvider) CreateSamplingSut(
        double failureRateThreshold = 0.5,
        int minimumThroughput = 2,
        int permittedCallsInHalfOpen = 1,
        TimeSpan? breakDuration = null) {

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        SamplingWindowCircuitBreakerOptions options = new() {
            FailureRateThreshold = failureRateThreshold,
            MinimumThroughput = minimumThroughput,
            PermittedNumberOfCallsInHalfOpenState = permittedCallsInHalfOpen,
            SamplingWindow = TimeSpan.FromSeconds(30),
            BreakDuration = breakDuration ?? TimeSpan.FromSeconds(30)
        };

        SamplingWindowCircuitBreaker breaker = new(
            CreateCounterFactory(timeProvider),
            options,
            timeProvider,
            NullLogger<SamplingWindowCircuitBreaker>.Instance);

        return (breaker, timeProvider);
    }

    private static async Task DriveToTripAsync(ICircuitBreaker breaker, string key, int failures) {
        for(int i = 0; i < failures; i++) {
            await breaker.OnFailureAsync(key, TestContext.Current.CancellationToken);
        }
    }

    public sealed class TheConsecutiveFailuresStateRead {
        [Fact]
        public async Task GetStateAsync_WhenNothingHasFailed_ReportsClosed() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateConsecutiveSut();

            CircuitState state = await breaker.GetStateAsync("cf-state-closed", TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Closed, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenThresholdReached_ReportsOpen() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateConsecutiveSut(failureThreshold: 3);
            const string key = "cf-state-open";

            await DriveToTripAsync(breaker, key, 3);

            CircuitState state = await breaker.GetStateAsync(key, TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Open, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenBreakDurationElapsed_ReportsHalfOpen() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider time) =
                CreateConsecutiveSut(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30));
            const string key = "cf-state-halfopen";

            await DriveToTripAsync(breaker, key, 3);
            time.Advance(TimeSpan.FromSeconds(31));

            CircuitState state = await breaker.GetStateAsync(key, TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.HalfOpen, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenHalfOpen_DoesNotConsumeTheProbe() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider time) =
                CreateConsecutiveSut(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30));
            const string key = "cf-state-probe-preserved";

            await DriveToTripAsync(breaker, key, 3);
            time.Advance(TimeSpan.FromSeconds(31));

            for(int i = 0; i < 5; i++) {
                Assert.Equal(CircuitState.HalfOpen, await breaker.GetStateAsync(key, TestContext.Current.CancellationToken));
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, decision.State);
        }

        [Fact]
        public async Task GetStateAsync_WhenProbeAlreadyClaimedByAnotherCaller_StillReportsHalfOpen() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider time) =
                CreateConsecutiveSut(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30));
            const string key = "cf-state-probe-taken";

            await DriveToTripAsync(breaker, key, 3);
            time.Advance(TimeSpan.FromSeconds(31));

            CircuitExecutionDecision probe = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, probe.State);

            CircuitState state = await breaker.GetStateAsync(key, TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.HalfOpen, state);
        }

        [Fact]
        public async Task GetStateAsync_AfterRecovery_ReportsClosed() {
            (ConsecutiveFailuresCircuitBreaker breaker, FakeTimeProvider time) =
                CreateConsecutiveSut(failureThreshold: 3, breakDuration: TimeSpan.FromSeconds(30));
            const string key = "cf-state-recovered";

            await DriveToTripAsync(breaker, key, 3);
            time.Advance(TimeSpan.FromSeconds(31));
            await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);
            await breaker.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            CircuitState state = await breaker.GetStateAsync(key, TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Closed, state);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetStateAsync_GivenInvalidKey_ThrowsArgumentException(string? invalidKey) {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateConsecutiveSut();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                breaker.GetStateAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task GetStateAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceledException() {
            (ConsecutiveFailuresCircuitBreaker breaker, _) = CreateConsecutiveSut();

            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                breaker.GetStateAsync("cf-state-cancelled", cts.Token).AsTask());
        }
    }

    public sealed class TheSamplingWindowStateRead {
        [Fact]
        public async Task GetStateAsync_WhenNothingHasFailed_ReportsClosed() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSamplingSut();

            CircuitState state = await breaker.GetStateAsync("sw-state-closed", TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Closed, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenFailureRateTrippedTheCircuit_ReportsOpen() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSamplingSut(failureRateThreshold: 0.5, minimumThroughput: 2);
            const string key = "sw-state-open";

            await DriveToTripAsync(breaker, key, 2);

            CircuitState state = await breaker.GetStateAsync(key, TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Open, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenBreakDurationElapsed_ReportsHalfOpenWithoutConsumingTheProbe() {
            (SamplingWindowCircuitBreaker breaker, FakeTimeProvider time) = CreateSamplingSut(
                failureRateThreshold: 0.5,
                minimumThroughput: 2,
                permittedCallsInHalfOpen: 1,
                breakDuration: TimeSpan.FromSeconds(30));
            const string key = "sw-state-probe-preserved";

            await DriveToTripAsync(breaker, key, 2);
            time.Advance(TimeSpan.FromSeconds(31));

            for(int i = 0; i < 5; i++) {
                Assert.Equal(CircuitState.HalfOpen, await breaker.GetStateAsync(key, TestContext.Current.CancellationToken));
            }

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, decision.State);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetStateAsync_GivenInvalidKey_ThrowsArgumentException(string? invalidKey) {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSamplingSut();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                breaker.GetStateAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task GetStateAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceledException() {
            (SamplingWindowCircuitBreaker breaker, _) = CreateSamplingSut();

            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                breaker.GetStateAsync("sw-state-cancelled", cts.Token).AsTask());
        }
    }

    public sealed class TheCompositeStateRead {
        [Fact]
        public async Task GetStateAsync_WhenEveryTierIsClosed_ReportsClosed() {
            StubCircuitBreaker tier1 = new(CircuitState.Closed);
            StubCircuitBreaker tier2 = new(CircuitState.Closed);
            CompositeCircuitBreaker composite = new(tier1, tier2);

            CircuitState state = await composite.GetStateAsync("composite-closed", TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Closed, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenAnyTierIsHalfOpen_ReportsHalfOpen() {
            StubCircuitBreaker tier1 = new(CircuitState.Closed);
            StubCircuitBreaker tier2 = new(CircuitState.HalfOpen);
            StubCircuitBreaker tier3 = new(CircuitState.Closed);
            CompositeCircuitBreaker composite = new(tier1, tier2, tier3);

            CircuitState state = await composite.GetStateAsync("composite-halfopen", TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.HalfOpen, state);
        }

        [Fact]
        public async Task GetStateAsync_WhenAnyTierIsOpen_ReportsOpenAndShortCircuitsRemainingTiers() {
            StubCircuitBreaker tier1 = new(CircuitState.HalfOpen);
            StubCircuitBreaker tier2 = new(CircuitState.Open);
            StubCircuitBreaker tier3 = new(CircuitState.Closed);
            CompositeCircuitBreaker composite = new(tier1, tier2, tier3);

            CircuitState state = await composite.GetStateAsync("composite-open", TestContext.Current.CancellationToken);

            Assert.Equal(CircuitState.Open, state);
            Assert.Equal(0, tier3.GetStateCount);
        }

        [Fact]
        public async Task GetStateAsync_NeverAcquiresFromAnyTier() {
            StubCircuitBreaker tier1 = new(CircuitState.HalfOpen);
            StubCircuitBreaker tier2 = new(CircuitState.HalfOpen);
            CompositeCircuitBreaker composite = new(tier1, tier2);

            await composite.GetStateAsync("composite-no-acquire", TestContext.Current.CancellationToken);

            Assert.Equal(0, tier1.AcquireCount);
            Assert.Equal(0, tier2.AcquireCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetStateAsync_GivenInvalidKey_ThrowsArgumentException(string? invalidKey) {
            CompositeCircuitBreaker composite = new(new StubCircuitBreaker(CircuitState.Closed));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                composite.GetStateAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task GetStateAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceledException() {
            CompositeCircuitBreaker composite = new(new StubCircuitBreaker(CircuitState.Closed));

            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                composite.GetStateAsync("composite-cancelled", cts.Token).AsTask());
        }
    }

    private sealed class StubCircuitBreaker(CircuitState state) : ICircuitBreaker {
        private int _acquireCount;
        private int _getStateCount;

        public int AcquireCount => Volatile.Read(ref this._acquireCount);
        public int GetStateCount => Volatile.Read(ref this._getStateCount);

        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._acquireCount);
            return ValueTask.FromResult(CircuitExecutionDecision.Allowed());
        }

        public ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._getStateCount);
            return ValueTask.FromResult(state);
        }

        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
