namespace Wiaoj.Resilience.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "CompositeCircuitBreaker")]
public sealed class CompositeCircuitBreakerTests {

    public sealed class TheConstructorValidation {
        [Fact]
        public void GivenNullBreakers_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new CompositeCircuitBreaker(null!));
        }

        [Fact]
        public void GivenEmptyBreakers_ThrowsArgumentException() {
            Assert.ThrowsAny<ArgumentException>(() =>
                new CompositeCircuitBreaker([]));
        }

        [Fact]
        public void GivenNullLogger_ThrowsArgumentNullException() {
            MockCircuitBreaker mock = new(CircuitExecutionDecision.Allowed());
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new CompositeCircuitBreaker([mock], null!));
        }
    }

    public sealed class TheMultiTierEvaluation {
        [Fact]
        public async Task WhenAllTiersAreClosed_ReturnsAllowed() {
            // Arrange: 3 tiers all operational in Closed state
            MockCircuitBreaker tier1 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier2 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier3 = new(CircuitExecutionDecision.Allowed());

            CompositeCircuitBreaker composite = new(tier1, tier2, tier3);
            const string key = "service_composite_healthy";

            // Act
            CircuitExecutionDecision decision = await composite.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
            Assert.Null(decision.RetryAfter);

            Assert.Equal(1, tier1.AcquireCount);
            Assert.Equal(1, tier2.AcquireCount);
            Assert.Equal(1, tier3.AcquireCount);
        }

        [Fact]
        public async Task WhenAnyTierIsOpen_ReturnsDeniedWithMaxRetryAfter_AndShortCircuitsSubsequentTiers() {
            // Arrange: Tier 1 is Closed, Tier 2 is Open (30s wait), Tier 3 should never be evaluated
            MockCircuitBreaker tier1 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier2 = new(CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(30)));
            MockCircuitBreaker tier3 = new(CircuitExecutionDecision.Allowed());

            CompositeCircuitBreaker composite = new(tier1, tier2, tier3);
            const string key = "service_composite_broken";

            // Act
            CircuitExecutionDecision decision = await composite.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            // Assert: Fast-fails at tier 2
            Assert.False(decision.IsAllowed);
            Assert.Equal(CircuitState.Open, decision.State);
            Assert.NotNull(decision.RetryAfter);
            Assert.Equal(TimeSpan.FromSeconds(30), decision.RetryAfter.Value);

            Assert.Equal(1, tier1.AcquireCount);
            Assert.Equal(1, tier2.AcquireCount);
            Assert.Equal(0, tier3.AcquireCount); // Short-circuited!
        }

        [Fact]
        public async Task WhenAllTiersPermitAndAnyTierIsHalfOpen_ReturnsHalfOpenProbe() {
            // Arrange: Tier 1 is Closed, Tier 2 is in HalfOpen trial probe state
            MockCircuitBreaker tier1 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier2 = new(CircuitExecutionDecision.HalfOpenProbe());

            CompositeCircuitBreaker composite = new(tier1, tier2);
            const string key = "service_composite_probing";

            // Act
            CircuitExecutionDecision decision = await composite.TryAcquireAsync(key, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.HalfOpen, decision.State);
        }
    }

    public sealed class TheEventBroadcasting {
        [Fact]
        public async Task OnSuccessAsync_BroadcastsSuccessToAllUnderlyingTiers() {
            MockCircuitBreaker tier1 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier2 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier3 = new(CircuitExecutionDecision.Allowed());

            CompositeCircuitBreaker composite = new(tier1, tier2, tier3);
            const string key = "service_broadcast_success";

            // Act
            await composite.OnSuccessAsync(key, TestContext.Current.CancellationToken);

            // Assert: All tiers received OnSuccess
            Assert.Equal(1, tier1.SuccessCount);
            Assert.Equal(1, tier2.SuccessCount);
            Assert.Equal(1, tier3.SuccessCount);
        }

        [Fact]
        public async Task OnFailureAsync_BroadcastsFailureToAllUnderlyingTiers() {
            MockCircuitBreaker tier1 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier2 = new(CircuitExecutionDecision.Allowed());
            MockCircuitBreaker tier3 = new(CircuitExecutionDecision.Allowed());

            CompositeCircuitBreaker composite = new(tier1, tier2, tier3);
            const string key = "service_broadcast_failure";

            // Act
            await composite.OnFailureAsync(key, TestContext.Current.CancellationToken);

            // Assert: All tiers received OnFailure
            Assert.Equal(1, tier1.FailureCount);
            Assert.Equal(1, tier2.FailureCount);
            Assert.Equal(1, tier3.FailureCount);
        }
    }

    public sealed class TheHighVolumeTiersStressTest {
        [Fact]
        public async Task Given100ClosedTiers_EvaluatesAll100Tiers() {
            const int totalTiers = 100;
            MockCircuitBreaker[] tiers = new MockCircuitBreaker[totalTiers];

            for(int i = 0; i < totalTiers; i++) {
                tiers[i] = new MockCircuitBreaker(CircuitExecutionDecision.Allowed());
            }

            CompositeCircuitBreaker composite = new(tiers);
            CircuitExecutionDecision decision = await composite.TryAcquireAsync("stress_closed", TestContext.Current.CancellationToken);

            Assert.True(decision.IsAllowed);
            Assert.All(tiers, t => Assert.Equal(1, t.AcquireCount));
        }

        [Fact]
        public async Task Given100Tiers_WhenTier42IsOpen_ShortCircuitsRemaining58Tiers() {
            const int totalTiers = 100;
            MockCircuitBreaker[] tiers = new MockCircuitBreaker[totalTiers];

            for(int i = 0; i < totalTiers; i++) {
                if(i == 41) // 42nd tier (0-indexed)
                {
                    tiers[i] = new MockCircuitBreaker(CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(45)));
                }
                else {
                    tiers[i] = new MockCircuitBreaker(CircuitExecutionDecision.Allowed());
                }
            }

            CompositeCircuitBreaker composite = new(tiers);
            CircuitExecutionDecision decision = await composite.TryAcquireAsync("stress_deny", TestContext.Current.CancellationToken);

            Assert.False(decision.IsAllowed);
            Assert.Equal(TimeSpan.FromSeconds(45), decision.RetryAfter);

            for(int i = 0; i < 42; i++) {
                Assert.Equal(1, tiers[i].AcquireCount);
            }
            for(int i = 42; i < 100; i++) {
                Assert.Equal(0, tiers[i].AcquireCount);
            }
        }
    }

    public sealed class TheArgumentAndCancellationGuards {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Methods_ThrowArgumentException_OnInvalidKey(string? invalidKey) {
            MockCircuitBreaker mock = new(CircuitExecutionDecision.Allowed());
            CompositeCircuitBreaker composite = new(mock);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => composite.TryAcquireAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => composite.OnSuccessAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => composite.OnFailureAsync(invalidKey!, TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task TryAcquireAsync_ThrowsOperationCanceledException_WhenTokenAlreadyCancelled() {
            MockCircuitBreaker mock = new(CircuitExecutionDecision.Allowed());
            CompositeCircuitBreaker composite = new(mock);

            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                composite.TryAcquireAsync("key_cancel", cts.Token).AsTask());
        }
    }

    private sealed class MockCircuitBreaker(CircuitExecutionDecision decision) : ICircuitBreaker {
        private int _acquireCount;
        private int _successCount;
        private int _failureCount;

        public int AcquireCount => Volatile.Read(ref this._acquireCount);
        public int SuccessCount => Volatile.Read(ref this._successCount);
        public int FailureCount => Volatile.Read(ref this._failureCount);

        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._acquireCount);
            return ValueTask.FromResult(decision);
        }

        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._successCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._failureCount);
            return ValueTask.CompletedTask;
        }
    }
}