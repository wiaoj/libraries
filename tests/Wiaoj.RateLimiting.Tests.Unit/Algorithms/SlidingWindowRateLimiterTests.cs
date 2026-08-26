using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "SlidingWindow")]
public sealed class SlidingWindowRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenNullFactory_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() => new SlidingWindowRateLimiter(null!, "policy", 10, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GivenNullOrWhitespacePolicyName_ThrowsArgumentException(string? invalidPolicy) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentException>(() => new SlidingWindowRateLimiter(context.Factory, invalidPolicy!, 10, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeLimit_ThrowsArgumentOutOfRangeException(int invalidLimit) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new SlidingWindowRateLimiter(context.Factory, "policy", invalidLimit, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeWindow_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new SlidingWindowRateLimiter(context.Factory, "policy", 10, TimeSpan.FromTicks(invalidTicks)));
        }
    }

    public sealed class TheTryAcquireArgumentValidation {

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            DistributedCounterTestContext context = new();
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 10, window: TimeSpan.FromMinutes(1));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => limiter.TryAcquireAsync(invalidKey!, cost: 1, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            DistributedCounterTestContext context = new();
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 10, window: TimeSpan.FromMinutes(1));

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
                () => limiter.TryAcquireAsync("client_1", invalidCost, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheSlidingWindowMathAndWeighting {

        [Fact]
        public async Task InFreshWindow_WithNoPreviousWindow_AllowsUpToFullLimit() {
            // Arrange: 10 requests per 60-second window
            FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            DistributedCounterTestContext context = new(timeProvider);
            TimeSpan window = TimeSpan.FromSeconds(60);
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 10, window: window, timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_1";

            // Act: 6 requests in fresh window (Previous window is 0)
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 6, ct);

            // Assert
            Assert.True(d1.IsAllowed);
            Assert.Equal(4, d1.Remaining); // 10 - 6 = 4
        }

        [Fact]
        public async Task InSubsequentWindow_BlendsPreviousWindowCountBasedOnElapsedTimeWeight() {
            // Arrange: 10 requests per 60-second window
            DateTimeOffset startTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(startTime);
            DistributedCounterTestContext context = new(timeProvider);
            TimeSpan window = TimeSpan.FromSeconds(60);
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 10, window: window, timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_weighted";

            // 1. Window 0 (12:00:00): Consume 8 units
            RateLimitDecision w0 = await limiter.TryAcquireAsync(key, cost: 8, ct);
            Assert.True(w0.IsAllowed);

            // 2. Advance time 30 seconds into Window 1 (12:01:30)
            // Elapsed in Window 1 = 30s. Previous window weight = 1.0 - (30/60) = 0.5
            // Previous window contributes: 8 * 0.5 = 4 units
            timeProvider.Advance(TimeSpan.FromSeconds(90)); // 60s + 30s

            // Act: Request 4 units in Window 1
            // Estimated total = (8 * 0.5) + 4 = 8 <= 10 -> Allowed!
            RateLimitDecision w1 = await limiter.TryAcquireAsync(key, cost: 4, ct);

            // Assert
            Assert.True(w1.IsAllowed);
            Assert.Equal(2, w1.Remaining); // 10 - 8 = 2 remaining
        }

        [Fact]
        public async Task RequestWithCostGreaterThanLimit_IsDeniedImmediatelyWithoutAlteringState() {
            // Arrange: Max limit is 5
            DistributedCounterTestContext context = new();
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 5, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_overflow";

            // Act: Request 10 units (exceeds limit 5)
            RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 10, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(0, decision.Remaining);
        }

        [Fact]
        public async Task AfterTwoFullWindowsElapsed_PreviousWindowWeightBecomesZeroAndAllowsFullLimit() {
            // Arrange
            DateTimeOffset startTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(startTime);
            DistributedCounterTestContext context = new(timeProvider);
            TimeSpan window = TimeSpan.FromSeconds(60);
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 10, window: window, timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_twowindows";

            // Exhaust window 0
            await limiter.TryAcquireAsync(key, cost: 10, ct);

            // Advance time past 2 full windows (130 seconds)
            timeProvider.Advance(TimeSpan.FromSeconds(130));

            // Act
            RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 10, ct);

            // Assert: Full capacity completely available
            Assert.True(decision.IsAllowed);
            Assert.Equal(0, decision.Remaining);
        }
    }

    public sealed class TheSpeculativeRollbackOnDenial {

        [Fact]
        public async Task WhenEstimatedTotalExceedsLimit_RollsBackIncrementAndDoesNotLeakCapacity() {
            // Arrange: 10 requests per 60-second window
            DateTimeOffset startTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(startTime);
            DistributedCounterTestContext context = new(timeProvider);
            TimeSpan window = TimeSpan.FromSeconds(60);
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 10, window: window, timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_rollback";

            // Window 0: Consume 10 units (Full limit)
            await limiter.TryAcquireAsync(key, cost: 10, ct);

            // Advance 15 seconds into Window 1 (12:01:15)
            // Previous weight = (60 - 15) / 60 = 0.75
            // Previous contribution = 10 * 0.75 = 7.5 units
            timeProvider.Advance(TimeSpan.FromSeconds(75));

            // Act 1: Request 4 units
            // Estimated = 7.5 + 4 = 11.5 > 10 -> Must be DENIED and ROLLED BACK!
            RateLimitDecision denied = await limiter.TryAcquireAsync(key, cost: 4, ct);

            // Assert 1: Denied with accurate retry after (60s - 15s = 45s remaining)
            Assert.False(denied.IsAllowed);
            Assert.Equal(0, denied.Remaining);
            Assert.NotNull(denied.RetryAfter);
            Assert.Equal(45, (int)Math.Round(denied.RetryAfter.Value.TotalSeconds));

            // Act 2: Verify Rollback Succeeded
            // Because rollback succeeded, current counter is 0, so 7.5 + 2 = 9.5 <= 10 -> Must be ALLOWED!
            RateLimitDecision allowedAfterRollback = await limiter.TryAcquireAsync(key, cost: 2, ct);

            // Assert 2
            Assert.True(allowedAfterRollback.IsAllowed);
            Assert.Equal(0, allowedAfterRollback.Remaining); // 10 - 9.5 = 0.5 (integer cast: 0)
        }
    }

    public sealed class TheCancellationBehavior {

        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            DistributedCounterTestContext context = new();
            SlidingWindowRateLimiter limiter = new(context.Factory, "api", limit: 5, window: TimeSpan.FromMinutes(1));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => limiter.TryAcquireAsync("client_precancelled", cost: 1, cts.Token).AsTask());
        }
    }

    public sealed class TheKeyAndPolicyIsolation {

        [Fact]
        public async Task SameKeyAcrossDifferentPolicies_MaintainsIndependentWindowBuckets() {
            // Arrange
            DistributedCounterTestContext context = new();
            SlidingWindowRateLimiter authLimiter = new(context.Factory, "auth_sliding", limit: 5, window: TimeSpan.FromMinutes(1));
            SlidingWindowRateLimiter searchLimiter = new(context.Factory, "search_sliding", limit: 10, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string clientIp = "10.0.0.1";

            // Act: Exhaust auth
            await authLimiter.TryAcquireAsync(clientIp, cost: 5, ct);
            RateLimitDecision authDenied = await authLimiter.TryAcquireAsync(clientIp, cost: 1, ct);

            // Act: Search must still have full quota
            RateLimitDecision searchAllowed = await searchLimiter.TryAcquireAsync(clientIp, cost: 5, ct);

            // Assert
            Assert.False(authDenied.IsAllowed);
            Assert.True(searchAllowed.IsAllowed);
            Assert.Equal(5, searchAllowed.Remaining);
        }

        [Fact]
        public async Task DifferentKeysUnderSamePolicy_MaintainIndependentQuotas() {
            DistributedCounterTestContext context = new();
            SlidingWindowRateLimiter limiter = new(context.Factory, "shared_sliding", limit: 4, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Exhaust key A
            await limiter.TryAcquireAsync("client_a", cost: 4, ct);
            RateLimitDecision aDenied = await limiter.TryAcquireAsync("client_a", cost: 1, ct);

            // Key B remains untouched
            RateLimitDecision bAllowed = await limiter.TryAcquireAsync("client_b", cost: 4, ct);

            Assert.False(aDenied.IsAllowed);
            Assert.True(bAllowed.IsAllowed);
            Assert.Equal(0, bAllowed.Remaining);
        }
    }
}