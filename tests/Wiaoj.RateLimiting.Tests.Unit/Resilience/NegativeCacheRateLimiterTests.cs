using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
[Trait("Feature", "NegativeCache")]
public sealed class NegativeCacheRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenNullInnerAlgorithm_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() => new NegativeCacheRateLimiter(null!));
        }

        [Fact]
        public void GivenNullTimeProvider_ThrowsArgumentNullException() {
            MockRateLimitAlgorithm inner = new();
            Assert.ThrowsAny<ArgumentNullException>(() => new NegativeCacheRateLimiter(inner, null!));
        }
    }

    public sealed class TheNegativeCacheShortCircuiting {

        [Fact]
        public async Task WhenInnerDeniesWithRetryAfter_CachesDenialAndShortCircuitsSubsequentRequests() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            MockRateLimitAlgorithm inner = new();
            NegativeCacheRateLimiter negativeCache = new(inner, timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "spammer_ip";

            // Configure inner to deny with 30s retry after
            inner.SetOutcome(RateLimitDecision.Denied(TimeSpan.FromSeconds(30), remaining: 0));

            // Act 1: First request hits inner algorithm
            RateLimitDecision d1 = await negativeCache.TryAcquireAsync(key, cost: 1, ct);

            // Assert 1
            Assert.False(d1.IsAllowed);
            Assert.Equal(1, inner.CallCount);

            // Act 2: Advance time 10 seconds into the 30s ban window
            timeProvider.Advance(TimeSpan.FromSeconds(10));

            // Second request must be short-circuited in RAM (Zero call to inner!)
            RateLimitDecision d2 = await negativeCache.TryAcquireAsync(key, cost: 1, ct);

            // Assert 2
            Assert.False(d2.IsAllowed);
            Assert.Equal(1, inner.CallCount); // Still 1! Inner algorithm was bypassed
            Assert.NotNull(d2.RetryAfter);
            Assert.Equal(20, (int)Math.Round(d2.RetryAfter.Value.TotalSeconds)); // 30s - 10s = 20s remaining

            // Act 3: Advance past the 30s ban window (21s more, total 31s)
            timeProvider.Advance(TimeSpan.FromSeconds(21));

            // Configure inner to allow on recovery
            inner.SetOutcome(RateLimitDecision.Allowed(remaining: 5));

            // Third request must now reach the inner algorithm again
            RateLimitDecision d3 = await negativeCache.TryAcquireAsync(key, cost: 1, ct);

            // Assert 3
            Assert.True(d3.IsAllowed);
            Assert.Equal(2, inner.CallCount); // Inner algorithm called again
        }

        [Fact]
        public async Task WhenInnerAllows_DoesNotCacheAndAlwaysPassesThrough() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            MockRateLimitAlgorithm inner = new();
            NegativeCacheRateLimiter negativeCache = new(inner, timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "legit_user";

            inner.SetOutcome(RateLimitDecision.Allowed(remaining: 10));

            // Act: 5 consecutive allowed requests
            for(int i = 0; i < 5; i++) {
                RateLimitDecision decision = await negativeCache.TryAcquireAsync(key, cost: 1, ct);
                Assert.True(decision.IsAllowed);
            }

            // Assert: All 5 reached the inner algorithm (no caching of allowed requests)
            Assert.Equal(5, inner.CallCount);
        }
    }

    public sealed class TheStateReset {

        [Fact]
        public async Task Reset_ClearsAllCachedDenialsImmediately() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            MockRateLimitAlgorithm inner = new();
            NegativeCacheRateLimiter negativeCache = new(inner, timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "cached_user";

            // Deny and cache for 10 minutes
            inner.SetOutcome(RateLimitDecision.Denied(TimeSpan.FromMinutes(10)));
            await negativeCache.TryAcquireAsync(key, cost: 1, ct);
            Assert.Equal(1, inner.CallCount);

            // Act: Reset cache
            negativeCache.Reset();

            // Next request should hit inner again instead of short-circuiting
            inner.SetOutcome(RateLimitDecision.Allowed(5));
            RateLimitDecision decision = await negativeCache.TryAcquireAsync(key, cost: 1, ct);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(2, inner.CallCount);
        }
    }

    private sealed class MockRateLimitAlgorithm : IRateLimitAlgorithm {
        private RateLimitDecision _outcome = RateLimitDecision.Allowed();
        private int _callCount;

        public int CallCount => Volatile.Read(ref this._callCount);

        public void SetOutcome(RateLimitDecision outcome) {
            this._outcome = outcome;
        }

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._callCount);
            return ValueTask.FromResult(this._outcome);
        }
    }
}