using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Tests.Unit.Fakes;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "TokenBucket")]
public sealed class TokenBucketRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter(invalidCapacity, TimeSpan.FromSeconds(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeWindow_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter(10, TimeSpan.FromTicks(invalidTicks)));
        }
    }

    public sealed class TheTryAcquireArgumentValidation {

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            TokenBucketRateLimiter limiter = new(capacity: 10, window: TimeSpan.FromSeconds(1));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => limiter.TryAcquireAsync(invalidKey!, cost: 1, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            TokenBucketRateLimiter limiter = new(capacity: 10, window: TimeSpan.FromSeconds(1));

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
                () => limiter.TryAcquireAsync("user_1", invalidCost, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheTokenReplenishmentAndBurstMath {

        [Fact]
        public async Task InitialState_StartsWithFullCapacityAndAllowsBurst() {
            // Arrange: 10 tokens per 10 seconds
            FakeTimeProvider timeProvider = new();
            TokenBucketRateLimiter limiter = new(capacity: 10, window: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_burst";

            // Act: Consume 7 tokens instantly
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 7, ct);

            // Assert
            Assert.True(d1.IsAllowed);
            Assert.Equal(3, d1.Remaining); // 10 - 7 = 3 tokens left
        }

        [Fact]
        public async Task DrainedBucket_RefillsTokensContinuouslyOverTime() {
            // Arrange: 10 tokens per 10 seconds (1 token per second)
            FakeTimeProvider timeProvider = new();
            TokenBucketRateLimiter limiter = new(capacity: 10, window: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_refill";

            // 1. Drain entire bucket
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 10, ct);
            Assert.True(d1.IsAllowed);
            Assert.Equal(0, d1.Remaining);

            // 2. Immediate request is denied with 1s retry after
            RateLimitDecision d2 = await limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.False(d2.IsAllowed);
            Assert.Equal(0, d2.Remaining);
            Assert.NotNull(d2.RetryAfter);
            Assert.Equal(1, (int)Math.Ceiling(d2.RetryAfter.Value.TotalSeconds));

            // 3. Advance time by 4 seconds (refills 4 tokens)
            timeProvider.Advance(TimeSpan.FromSeconds(4));

            // Act: Request 4 tokens
            RateLimitDecision d3 = await limiter.TryAcquireAsync(key, cost: 4, ct);

            // Assert
            Assert.True(d3.IsAllowed);
            Assert.Equal(0, d3.Remaining);
        }

        [Fact]
        public async Task Refill_NeverExceedsMaximumCapacity() {
            // Arrange: 5 tokens per 5 seconds
            FakeTimeProvider timeProvider = new();
            TokenBucketRateLimiter limiter = new(capacity: 5, window: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_cap";

            // Consume 2 tokens (3 left)
            await limiter.TryAcquireAsync(key, cost: 2, ct);

            // Advance 100 seconds (should cap at max 5, not 100!)
            timeProvider.Advance(TimeSpan.FromSeconds(100));

            // Act
            RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(4, decision.Remaining); // 5 - 1 = 4
        }

        [Fact]
        public async Task RequestCostGreaterThanCapacity_IsDeniedImmediately() {
            // Arrange: Max capacity is 5
            TokenBucketRateLimiter limiter = new(capacity: 5, window: TimeSpan.FromSeconds(5));
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Request cost of 10
            RateLimitDecision decision = await limiter.TryAcquireAsync("user_over", cost: 10, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(5, decision.Remaining);
        }
    }

    public sealed class TheConcurrencyAndThreadSafety {

        [Fact]
        public async Task ConcurrentAcquisitions_EnforceStrictBucketCapacityWithoutLeaking() {
            FakeTimeProvider timeProvider = new();
            TokenBucketRateLimiter limiter = new(capacity: 20, window: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_race";

            const int totalAttempts = 50;
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

            Assert.Equal(20, allowedCount);
            Assert.Equal(30, deniedCount);
        }
    }

    public sealed class TheCancellationBehavior {

        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            TokenBucketRateLimiter limiter = new(capacity: 10, window: TimeSpan.FromSeconds(1));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => limiter.TryAcquireAsync("user_precancelled", cost: 1, cts.Token).AsTask());
        }
    }

    public sealed class TheKeyIsolation {

        [Fact]
        public async Task DifferentKeys_MaintainCompletelyIndependentBucketStates() {
            FakeTimeProvider timeProvider = new();
            TokenBucketRateLimiter limiter = new(capacity: 5, window: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Drain user A
            await limiter.TryAcquireAsync("user_a", cost: 5, ct);
            RateLimitDecision aDenied = await limiter.TryAcquireAsync("user_a", cost: 1, ct);

            // User B is fresh
            RateLimitDecision bAllowed = await limiter.TryAcquireAsync("user_b", cost: 5, ct);

            Assert.False(aDenied.IsAllowed);
            Assert.True(bAllowed.IsAllowed);
            Assert.Equal(0, bAllowed.Remaining);
        }
    }

    public sealed class TheStateReset {

        [Fact]
        public async Task Reset_ClearsAllTrackedBucketState() {
            // Arrange
            TokenBucketRateLimiter limiter = new(capacity: 5, window: TimeSpan.FromSeconds(5));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "user_reset";

            // Drain bucket
            await limiter.TryAcquireAsync(key, cost: 5, ct);
            Assert.False((await limiter.TryAcquireAsync(key, cost: 1, ct)).IsAllowed);

            // Act: Reset state
            limiter.Reset();

            // Assert: Bucket is full again
            RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 5, ct);
            Assert.True(decision.IsAllowed);
        }
    }

    public sealed class TheClockSkewAndNtpImmunity {

        [Fact]
        public async Task WhenSystemClockJumpsBackward_TokenRefillDoesNotFreeze() {
            // Arrange
            FakeTimeProvider fakeTime = new();
            ClockSkewTimeProvider timeProvider = new(fakeTime);
            TokenBucketRateLimiter limiter = new(capacity: 10, window: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "token_ntp_freeze";

            // Drain all tokens at T0
            RateLimitDecision initial = await limiter.TryAcquireAsync(key, cost: 10, ct);
            Assert.True(initial.IsAllowed);

            // Act
            // System wall-clock jumps backward by 1 hour
            timeProvider.WallClockOffset = TimeSpan.FromHours(-1);

            // 5 seconds elapse in physical monotonic time (should refill 5 tokens)
            fakeTime.Advance(TimeSpan.FromSeconds(5));

            RateLimitDecision afterSkew = await limiter.TryAcquireAsync(key, cost: 5, ct);

            // Assert
            Assert.True(afterSkew.IsAllowed);
            Assert.Equal(0, afterSkew.Remaining);
        }
    }
}