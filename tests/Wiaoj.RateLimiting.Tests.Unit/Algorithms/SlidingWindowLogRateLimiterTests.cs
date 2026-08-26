using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "SlidingWindowLog")]
public sealed class SlidingWindowLogRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeLimit_ThrowsArgumentOutOfRangeException(int invalidLimit) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new SlidingWindowLogRateLimiter(invalidLimit, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeWindow_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new SlidingWindowLogRateLimiter(10, TimeSpan.FromTicks(invalidTicks)));
        }
    }

    public sealed class TheTryAcquireArgumentValidation {

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            SlidingWindowLogRateLimiter limiter = new(limit: 10, window: TimeSpan.FromMinutes(1));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => limiter.TryAcquireAsync(invalidKey!, cost: 1, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            SlidingWindowLogRateLimiter limiter = new(limit: 10, window: TimeSpan.FromMinutes(1));

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
                () => limiter.TryAcquireAsync("user_1", invalidCost, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheExactLogEvictionAndRollingWindow {

        [Fact]
        public async Task EntriesExpireIndependently_AsIndividualTimestampsPassRollingWindow() {
            // Arrange: Max 3 requests within any rolling 30-second window
            DateTimeOffset startTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(startTime);
            SlidingWindowLogRateLimiter limiter = new(limit: 3, window: TimeSpan.FromSeconds(30), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_log";

            // 1. Request 1 at t=0s (12:00:00)
            await limiter.TryAcquireAsync(key, cost: 1, ct);

            // 2. Request 2 at t=10s (12:00:10)
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            await limiter.TryAcquireAsync(key, cost: 1, ct);

            // 3. Request 3 at t=20s (12:00:20)
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            await limiter.TryAcquireAsync(key, cost: 1, ct);

            // At t=20s, full limit (3/3) is reached -> Request 4 must be denied!
            RateLimitDecision deniedAt20s = await limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.False(deniedAt20s.IsAllowed);
            // Oldest entry was at t=0s, so window clears that entry at t=30s (10s remaining from t=20s)
            Assert.NotNull(deniedAt20s.RetryAfter);
            Assert.Equal(10, (int)Math.Ceiling(deniedAt20s.RetryAfter.Value.TotalSeconds));

            // Act: Advance to t=31s (Request 1 at t=0s is now expired, but Requests 2 & 3 are still active!)
            timeProvider.Advance(TimeSpan.FromSeconds(11)); // from 20s to 31s

            // Assert: Exactly 1 slot recovered!
            RateLimitDecision allowedAt31s = await limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.True(allowedAt31s.IsAllowed);
            Assert.Equal(0, allowedAt31s.Remaining); // 3 - (Req2 + Req3 + Current) = 0

            // Another request at t=31s must be denied (because Req2 at t=10s only expires at t=40s)
            RateLimitDecision deniedAt31s = await limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.False(deniedAt31s.IsAllowed);
            Assert.NotNull(deniedAt31s.RetryAfter);
            Assert.Equal(9, (int)Math.Ceiling(deniedAt31s.RetryAfter.Value.TotalSeconds)); // 40s - 31s = 9s
        }

        [Fact]
        public async Task DeniedRequests_AreNeverAppendedToLog() {
            // Arrange: Limit of 2 per 60s
            FakeTimeProvider timeProvider = new();
            SlidingWindowLogRateLimiter limiter = new(limit: 2, window: TimeSpan.FromSeconds(60), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_no_append";

            // Max out limit (2/2)
            await limiter.TryAcquireAsync(key, cost: 2, ct);

            // Spam 5 denied requests
            for(int i = 0; i < 5; i++) {
                RateLimitDecision denied = await limiter.TryAcquireAsync(key, cost: 1, ct);
                Assert.False(denied.IsAllowed);
            }

            // Advance past the 60s window
            timeProvider.Advance(TimeSpan.FromSeconds(61));

            // Act: If denied requests had leaked into log, this would fail
            RateLimitDecision allowed = await limiter.TryAcquireAsync(key, cost: 2, ct);

            // Assert: Log only contained the original 2, which are now expired
            Assert.True(allowed.IsAllowed);
            Assert.Equal(0, allowed.Remaining);
        }

        [Fact]
        public async Task RequestCostGreaterThanLimit_IsDeniedImmediatelyWithoutAlteringLog() {
            // Arrange: Create limiter with limit of 5
            SlidingWindowLogRateLimiter limiter = new(limit: 5, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act 1: Request with cost of 6 exceeds max limit (6 > 5) -> Must be denied
            RateLimitDecision decision = await limiter.TryAcquireAsync("user_overflow", cost: 6, ct);

            // Assert 1: Denied with 0 remaining capacity
            Assert.False(decision.IsAllowed);
            Assert.Equal(0, decision.Remaining);

            // Act & Assert 2: Verify log was not mutated by the rejected request
            // If the cost of 6 had leaked into the log, this request of cost 5 would fail.
            RateLimitDecision nextValidRequest = await limiter.TryAcquireAsync("user_overflow", cost: 5, ct);
            Assert.True(nextValidRequest.IsAllowed);
            Assert.Equal(0, nextValidRequest.Remaining);
        }
    }

    public sealed class TheConcurrencyAndThreadSafety {

        [Fact]
        public async Task ConcurrentAcquisitions_EnforceExactLogCapacity() {
            FakeTimeProvider timeProvider = new();
            SlidingWindowLogRateLimiter limiter = new(limit: 15, window: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_concurrency";

            const int totalAttempts = 40;
            int allowedCount = 0;
            int deniedCount = 0;

            Task[] tasks = [.. Enumerable.Range(0, totalAttempts)
                .Select(_ => Task.Run(async () => {
                    RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 1, ct);
                    if(decision.IsAllowed) {
                        Interlocked.Increment(ref allowedCount);
                    }
                    else {
                        Interlocked.Increment(ref deniedCount);
                    }
                }, ct))];

            await Task.WhenAll(tasks);

            Assert.Equal(15, allowedCount);
            Assert.Equal(25, deniedCount);
        }
    }

    public sealed class TheCancellationBehavior {

        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            SlidingWindowLogRateLimiter limiter = new(limit: 5, window: TimeSpan.FromMinutes(1));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => limiter.TryAcquireAsync("user_precancelled", cost: 1, cts.Token).AsTask());
        }
    }

    public sealed class TheKeyIsolation {

        [Fact]
        public async Task DifferentKeys_MaintainIndependentTimestampLogs() {
            FakeTimeProvider timeProvider = new();
            SlidingWindowLogRateLimiter limiter = new(limit: 3, window: TimeSpan.FromMinutes(1), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;

            await limiter.TryAcquireAsync("user_1", cost: 3, ct);
            RateLimitDecision user1Denied = await limiter.TryAcquireAsync("user_1", cost: 1, ct);

            RateLimitDecision user2Allowed = await limiter.TryAcquireAsync("user_2", cost: 3, ct);

            Assert.False(user1Denied.IsAllowed);
            Assert.True(user2Allowed.IsAllowed);
            Assert.Equal(0, user2Allowed.Remaining);
        }
    }
}