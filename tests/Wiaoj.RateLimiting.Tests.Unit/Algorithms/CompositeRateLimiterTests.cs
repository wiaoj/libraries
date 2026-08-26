using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "Composite")]
public sealed class CompositeRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenNullAlgorithms_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() => new CompositeRateLimiter((IReadOnlyList<IRateLimitAlgorithm>)null!));
        }

        [Fact]
        public void GivenEmptyAlgorithms_ThrowsArgumentException() {
            Assert.ThrowsAny<ArgumentException>(() => new CompositeRateLimiter([]));
        }
    }

    public sealed class TheTryAcquireArgumentValidation {

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            MockRateLimitAlgorithm mock = new(RateLimitDecision.Allowed(remaining: 10));
            CompositeRateLimiter composite = new(mock);

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => composite.TryAcquireAsync(invalidKey!, cost: 1, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            MockRateLimitAlgorithm mock = new(RateLimitDecision.Allowed(remaining: 10));
            CompositeRateLimiter composite = new(mock);

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
                () => composite.TryAcquireAsync("client_1", invalidCost, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheMultiTierEvaluation {

        [Fact]
        public async Task WhenAllTiersAllow_ReturnsAllowedWithLowestRemainingCapacity() {
            // Arrange: 3 tiers with remaining capacity 100, 20, and 50
            MockRateLimitAlgorithm tier1 = new(RateLimitDecision.Allowed(remaining: 100));
            MockRateLimitAlgorithm tier2 = new(RateLimitDecision.Allowed(remaining: 20)); // Lowest
            MockRateLimitAlgorithm tier3 = new(RateLimitDecision.Allowed(remaining: 50));

            CompositeRateLimiter composite = new(tier1, tier2, tier3);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            RateLimitDecision decision = await composite.TryAcquireAsync("client_1", cost: 1, ct);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(20, decision.Remaining); // Must be the minimum capacity among all tiers
            Assert.Null(decision.RetryAfter);
        }

        [Fact]
        public async Task WhenAnyTierDenies_ReturnsDeniedWithMaxRetryAfterAndShortCircuits() {
            // Arrange: Tier 1 allows, Tier 2 denies with 30s wait, Tier 3 is never evaluated
            MockRateLimitAlgorithm tier1 = new(RateLimitDecision.Allowed(remaining: 100));
            MockRateLimitAlgorithm tier2 = new(RateLimitDecision.Denied(retryAfter: TimeSpan.FromSeconds(30), remaining: 0));
            MockRateLimitAlgorithm tier3 = new(RateLimitDecision.Allowed(remaining: 50));

            CompositeRateLimiter composite = new(tier1, tier2, tier3);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            RateLimitDecision decision = await composite.TryAcquireAsync("client_2", cost: 1, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(0, decision.Remaining);
            Assert.Equal(TimeSpan.FromSeconds(30), decision.RetryAfter);

            Assert.Equal(1, tier1.CallCount);
            Assert.Equal(1, tier2.CallCount);
            Assert.Equal(0, tier3.CallCount); // Short-circuited!
        }

        [Fact]
        public async Task CostAndKey_ArePropagatedAccuratelyToUnderlyingTiers() {
            string capturedKey = string.Empty;
            int capturedCost = 0;

            CallbackRateLimitAlgorithm tier = new((k, c) => {
                capturedKey = k;
                capturedCost = c;
                return ValueTask.FromResult(RateLimitDecision.Allowed(10));
            });

            CompositeRateLimiter composite = new(tier);
            await composite.TryAcquireAsync("forwarded_client", cost: 7, TestContext.Current.CancellationToken);

            Assert.Equal("forwarded_client", capturedKey);
            Assert.Equal(7, capturedCost);
        }
    }

    public sealed class TheHighVolumeTiersStressTest {

        [Fact]
        public async Task Given100PermittingTiers_EvaluatesAllTiersAndFindsMinimumRemaining() {
            int totalTiers = 100;
            MockRateLimitAlgorithm[] tiers = new MockRateLimitAlgorithm[totalTiers];

            for(int i = 0; i < totalTiers; i++) {
                long remaining = 1000 - i;
                tiers[i] = new MockRateLimitAlgorithm(RateLimitDecision.Allowed(remaining));
            }

            CompositeRateLimiter composite = new(tiers);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            RateLimitDecision decision = await composite.TryAcquireAsync("stress_client", cost: 1, ct);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(901, decision.Remaining); // Tier 99 has remaining = 1000 - 99 = 901
            Assert.All(tiers, t => Assert.Equal(1, t.CallCount));
        }

        [Fact]
        public async Task Given100Tiers_WhenTier73Denies_ShortCircuitsRemaining27Tiers() {
            int totalTiers = 100;
            MockRateLimitAlgorithm[] tiers = new MockRateLimitAlgorithm[totalTiers];

            for(int i = 0; i < totalTiers; i++) {
                if(i == 72) // 73rd tier (0-indexed)
                {
                    tiers[i] = new MockRateLimitAlgorithm(RateLimitDecision.Denied(TimeSpan.FromSeconds(45), remaining: 0));
                }
                else {
                    tiers[i] = new MockRateLimitAlgorithm(RateLimitDecision.Allowed(remaining: 500));
                }
            }

            CompositeRateLimiter composite = new(tiers);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            RateLimitDecision decision = await composite.TryAcquireAsync("stress_client_deny", cost: 1, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(TimeSpan.FromSeconds(45), decision.RetryAfter);

            for(int i = 0; i < 73; i++) {
                Assert.Equal(1, tiers[i].CallCount);
            }
            for(int i = 73; i < 100; i++) {
                Assert.Equal(0, tiers[i].CallCount);
            }
        }
    }

    public sealed class TheCancellationBehavior {

        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            MockRateLimitAlgorithm mock = new(RateLimitDecision.Allowed(10));
            CompositeRateLimiter composite = new(mock);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => composite.TryAcquireAsync("client_precancelled", cost: 1, cts.Token).AsTask());
        }
    }

    private sealed class MockRateLimitAlgorithm(RateLimitDecision outcome) : IRateLimitAlgorithm {
        private int _callCount;
        public int CallCount => Volatile.Read(ref this._callCount);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._callCount);
            return ValueTask.FromResult(outcome);
        }
    }

    private sealed class CallbackRateLimitAlgorithm(Func<string, int, ValueTask<RateLimitDecision>> callback) : IRateLimitAlgorithm {
        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default)
            => callback(key, cost);
    }
}