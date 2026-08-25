using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "FixedWindow")]
public sealed class FixedWindowRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenNullFactory_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() => new FixedWindowRateLimiter(null!, "policy", 10, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GivenNullOrWhitespacePolicyName_ThrowsArgumentException(string? invalidPolicy) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentException>(() => new FixedWindowRateLimiter(context.Factory, invalidPolicy!, 10, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeLimit_ThrowsArgumentOutOfRangeException(int invalidLimit) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new FixedWindowRateLimiter(context.Factory, "policy", invalidLimit, TimeSpan.FromMinutes(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeWindow_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new FixedWindowRateLimiter(context.Factory, "policy", 10, TimeSpan.FromTicks(invalidTicks)));
        }
    }

    public sealed class TheAcquisitionLimitsAndEdgeCases {

        [Fact]
        public async Task TryAcquire_WithinLimit_AllowsAndDecrementsRemainingCapacity() {
            // Arrange: 5 requests per minute under "auth" policy
            DistributedCounterTestContext context = new();
            FixedWindowRateLimiter limiter = new(context.Factory, "auth", limit: 5, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_ip_1";

            // Act & Assert
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 2, ct);
            Assert.True(d1.IsAllowed);
            Assert.Equal(3, d1.Remaining); // 5 - 2 = 3
            Assert.Null(d1.RetryAfter);

            RateLimitDecision d2 = await limiter.TryAcquireAsync(key, cost: 3, ct);
            Assert.True(d2.IsAllowed);
            Assert.Equal(0, d2.Remaining); // 3 - 3 = 0
            Assert.Null(d2.RetryAfter);
        }

        [Fact]
        public async Task TryAcquire_ExceedingLimit_DeniesAndReturnsTtlRetryAfter() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            DistributedCounterTestContext context = new(timeProvider);
            TimeSpan window = TimeSpan.FromSeconds(60);
            FixedWindowRateLimiter limiter = new(context.Factory, "auth", limit: 10, window: window);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_ip_2";

            // Consume full limit (10/10)
            RateLimitDecision allowed = await limiter.TryAcquireAsync(key, cost: 10, ct);
            Assert.True(allowed.IsAllowed);

            // Advance 15 seconds into the 60s window
            timeProvider.Advance(TimeSpan.FromSeconds(15));

            // Act: Attempt to acquire 1 more unit (10 + 1 = 11 > 10, Denied!)
            RateLimitDecision denied = await limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert
            Assert.False(denied.IsAllowed);
            Assert.Equal(0, denied.Remaining);
            Assert.NotNull(denied.RetryAfter);
            Assert.True(denied.RetryAfter.Value.TotalSeconds is > 40 and <= 45); // ~45s left in window
        }

        [Fact]
        public async Task TryAcquire_CostGreaterThanLimit_DeniesImmediatelyWithoutMutatingStorage() {
            // Arrange: Max limit is 5
            DistributedCounterTestContext context = new();
            FixedWindowRateLimiter limiter = new(context.Factory, "auth", limit: 5, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_ip_3";

            // Act: Request cost of 10 (exceeds limit 5)
            RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 10, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(0, decision.Remaining);

            // Storage must still be at 0, unaffected by rejected request
            IDistributedCounter counter = context.Factory.Create("auth", key);
            Assert.Equal(0, (await counter.GetValueAsync(ct)).Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task TryAcquire_GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            DistributedCounterTestContext context = new();
            FixedWindowRateLimiter limiter = new(context.Factory, "auth", limit: 10, window: TimeSpan.FromMinutes(1));

            await Assert.ThrowsAnyAsync<ArgumentException>(() => limiter.TryAcquireAsync(invalidKey!, 1, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task TryAcquire_GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            DistributedCounterTestContext context = new();
            FixedWindowRateLimiter limiter = new(context.Factory, "auth", limit: 10, window: TimeSpan.FromMinutes(1));

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => limiter.TryAcquireAsync("valid_key", invalidCost, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class ThePolicyAndKeyIsolation {

        [Fact]
        public async Task SameKeyAcrossDifferentPolicies_MaintainsCompletelyIndependentQuotas() {
            // Arrange: Same IP "192.168.1.50" accessing two different policies
            DistributedCounterTestContext context = new();
            FixedWindowRateLimiter authLimiter = new(context.Factory, "auth_policy", limit: 5, window: TimeSpan.FromMinutes(1));
            FixedWindowRateLimiter searchLimiter = new(context.Factory, "search_policy", limit: 20, window: TimeSpan.FromMinutes(1));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string clientIp = "192.168.1.50";

            // Act: Exhaust auth quota (5/5)
            await authLimiter.TryAcquireAsync(clientIp, cost: 5, ct);
            RateLimitDecision authDenied = await authLimiter.TryAcquireAsync(clientIp, cost: 1, ct);

            // Act: Search quota should still be 100% available!
            RateLimitDecision searchAllowed = await searchLimiter.TryAcquireAsync(clientIp, cost: 5, ct);

            // Assert: Complete policy isolation
            Assert.False(authDenied.IsAllowed);
            Assert.True(searchAllowed.IsAllowed);
            Assert.Equal(15, searchAllowed.Remaining); // 20 - 5 = 15
        }
    }
}