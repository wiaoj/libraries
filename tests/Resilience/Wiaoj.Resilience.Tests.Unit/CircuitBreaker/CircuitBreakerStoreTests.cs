using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Internal;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "CircuitBreakerStore")]
public sealed class CircuitBreakerStoreTests {

    private static (DistributedCircuitBreakerStore Store, FakeTimeProvider TimeProvider) CreateSut() {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddDistributedCounter(c => c.UseInMemory());

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IDistributedCounterFactory counterFactory = serviceProvider.GetRequiredService<IDistributedCounterFactory>();

        DistributedCircuitBreakerStore store = new(
            counterFactory,
            timeProvider,
            NullLogger<DistributedCircuitBreakerStore>.Instance);

        return (store, timeProvider);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CLOSED STATE EVALUATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheClosedState {
        [Fact]
        public async Task CanExecuteAsync_WhenFreshKey_ReturnsAllowedInClosedState() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            const string key = "endpoint-test-1";

            CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
            Assert.Null(decision.RetryAfter);
        }

        [Fact]
        public async Task RecordSuccessAsync_WhenInClosedState_ResetsFailureCounter() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            const string key = "endpoint-test-success";
            CircuitBreakerOptions options = new() { FailureThreshold = 5, BreakDuration = TimeSpan.FromMinutes(1) };

            // Record 2 failures (below threshold of 5)
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // Record success -> Should reset consecutive failures
            await store.RecordSuccessAsync(key, TestContext.Current.CancellationToken);

            // Subsequent failures should start from count 1 again
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. TRIPPING TO OPEN STATE
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheTrippingTransition {
        [Fact]
        public async Task RecordFailureAsync_WhenFailuresReachThreshold_TransitionsToOpen() {
            (DistributedCircuitBreakerStore store, _) = CreateSut();
            const string key = "endpoint-failing";
            CircuitBreakerOptions options = new() {
                FailureThreshold = 3,
                BreakDuration = TimeSpan.FromSeconds(30)
            };

            // Attempt 1: Fail (Count: 1)
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d1 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d1.IsAllowed);
            Assert.Equal(CircuitState.Closed, d1.State);

            // Attempt 2: Fail (Count: 2)
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d2 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.True(d2.IsAllowed);
            Assert.Equal(CircuitState.Closed, d2.State);

            // Attempt 3: Fail (Count: 3 >= Threshold) -> Should trip circuit to OPEN
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);
            CircuitExecutionDecision d3 = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

            Assert.False(d3.IsAllowed);
            Assert.Equal(CircuitState.Open, d3.State);
            Assert.NotNull(d3.RetryAfter);
            Assert.True(d3.RetryAfter.Value <= TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task CanExecuteAsync_WhileInOpenState_ReportsAccurateRemainingTtl() {
            (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "endpoint-ttl-check";
            CircuitBreakerOptions options = new() {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromSeconds(60)
            };

            // Trip circuit
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // Advance 20 seconds
            timeProvider.Advance(TimeSpan.FromSeconds(20));

            CircuitExecutionDecision decision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);

            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.NotNull(decision.RetryAfter);
            Assert.Equal(TimeSpan.FromSeconds(40), decision.RetryAfter.Value);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. HALF-OPEN AND RECOVERY PROBE
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheHalfOpenRecovery {
        [Fact]
        public async Task CanExecuteAsync_WhenBreakDurationExpires_AllowsSingleProbeInHalfOpenState() {
            (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "endpoint-recovering";
            CircuitBreakerOptions options = new() {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromSeconds(10)
            };

            // Trip circuit to OPEN
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // Advance past BreakDuration (11s)
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            // First probe request -> Allowed (Half-Open)
            CircuitExecutionDecision probeDecision = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.True(probeDecision.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, probeDecision.State);
        }

        [Fact]
        public async Task RecordSuccessAsync_WhenInHalfOpenState_ClosesCircuitAndResetsState() {
            (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "endpoint-recovery-success";
            CircuitBreakerOptions options = new() {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromSeconds(10)
            };

            // 1. Trip circuit
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // 2. Advance time past BreakDuration to enter Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(11));
            CircuitExecutionDecision probe = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, probe.State);

            // 3. Probe succeeds -> Record success
            await store.RecordSuccessAsync(key, TestContext.Current.CancellationToken);

            // 4. Circuit should now be CLOSED and healthy
            CircuitExecutionDecision postRecovery = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.True(postRecovery.IsAllowed);
            Assert.Equal(CircuitState.Closed, postRecovery.State);
            Assert.Null(postRecovery.RetryAfter);
        }

        [Fact]
        public async Task RecordFailureAsync_WhenInHalfOpenState_ReTripsCircuitToOpen() {
            (DistributedCircuitBreakerStore store, FakeTimeProvider timeProvider) = CreateSut();
            const string key = "endpoint-recovery-failed";
            CircuitBreakerOptions options = new() {
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromSeconds(20)
            };

            // 1. Trip circuit
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // 2. Advance time past BreakDuration to enter Half-Open
            timeProvider.Advance(TimeSpan.FromSeconds(21));
            CircuitExecutionDecision probe = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(CircuitState.HalfOpen, probe.State);

            // 3. Probe fails! Record failure during trial
            await store.RecordFailureAsync(key, options, TestContext.Current.CancellationToken);

            // 4. Circuit must re-trip immediately to OPEN for another BreakDuration (20s)
            CircuitExecutionDecision postFail = await store.CanExecuteAsync(key, TestContext.Current.CancellationToken);
            Assert.False(postFail.IsAllowed);
            Assert.Equal(CircuitState.Open, postFail.State);
            Assert.NotNull(postFail.RetryAfter);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. GUARD CLAUSES & INPUT VALIDATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheGuardClauses {
        [Fact]
        public void Constructor_Throws_WhenDependenciesAreNull() {
            (_, FakeTimeProvider timeProvider) = CreateSut();

            ServiceCollection services = new();
            services.AddSingleton<TimeProvider>(timeProvider);
            services.AddDistributedCounter(c => c.UseInMemory());
            IDistributedCounterFactory counterFactory = services.BuildServiceProvider().GetRequiredService<IDistributedCounterFactory>();

            Assert.ThrowsAny<ArgumentNullException>(() =>
                new DistributedCircuitBreakerStore(null!, timeProvider, NullLogger<DistributedCircuitBreakerStore>.Instance));

            Assert.ThrowsAny<ArgumentNullException>(() =>
                new DistributedCircuitBreakerStore(counterFactory, null!, NullLogger<DistributedCircuitBreakerStore>.Instance));

            Assert.ThrowsAny<ArgumentNullException>(() =>
                new DistributedCircuitBreakerStore(counterFactory, timeProvider, null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CanExecuteAsync_Throws_WhenKeyIsInvalid(string? invalidKey) {
            (DistributedCircuitBreakerStore store, _) = CreateSut();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                store.CanExecuteAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public void Options_Validate_Throws_OnInvalidConfigurations() {
            CircuitBreakerOptions options = new() { FailureThreshold = 0 };
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());

            options.FailureThreshold = 5;
            options.BreakDuration = TimeSpan.Zero;
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());

            options.BreakDuration = TimeSpan.FromSeconds(-10);
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
        }
    }
}