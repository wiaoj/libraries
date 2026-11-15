using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "SlidingWindow")]
public sealed class SlidingWindowRateLimiterHeadroomTests {

    private static (SlidingWindowRateLimiter Limiter, FakeTimeProvider Time) Create(int limit = 10) {
        FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        DistributedCounterTestContext context = new(time);
        return (new SlidingWindowRateLimiter(context.Factory, "headroom", limit, TimeSpan.FromSeconds(60), time), time);
    }

    [Fact]
    public async Task AKeyNobodySpent_HasTheWholeLimit_UntilTheWindowCloses() {
        (SlidingWindowRateLimiter limiter, FakeTimeProvider time) = Create();
        time.Advance(TimeSpan.FromSeconds(15));

        RateLimitHeadroom headroom = await limiter.PeekAsync("fresh", TestContext.Current.CancellationToken);

        Assert.Equal(new RateLimitHeadroom(10, 10, TimeSpan.FromSeconds(45)), headroom);
    }

    [Fact]
    public async Task Peeking_SpendsNothing_HoweverOften() {
        (SlidingWindowRateLimiter limiter, _) = Create(limit: 3);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await limiter.TryAcquireAsync("k", 2, ct);
        for(int i = 0; i < 5; i++) {
            Assert.Equal(1, (await limiter.PeekAsync("k", ct)).Remaining);
        }

        RateLimitDecision last = await limiter.TryAcquireAsync("k", 1, ct);
        Assert.True(last.IsAllowed);
        Assert.Equal(0, last.Remaining);
    }

    [Fact]
    public async Task ItReadsTheSameWeightedEstimate_AnAcquireIsRefusedBy() {
        (SlidingWindowRateLimiter limiter, FakeTimeProvider time) = Create();
        CancellationToken ct = TestContext.Current.CancellationToken;

        await limiter.TryAcquireAsync("w", 8, ct);
        time.Advance(TimeSpan.FromSeconds(75)); // the next window, a quarter in: 8 × 0.75 = 6 carried over
        await limiter.TryAcquireAsync("w", 1, ct);

        RateLimitHeadroom headroom = await limiter.PeekAsync("w", ct);

        Assert.Equal(3, headroom.Remaining);
        Assert.Equal(TimeSpan.FromSeconds(45), headroom.ResetAfter);
        Assert.Equal(headroom.Remaining, (await limiter.TryAcquireAsync("w", 1, ct)).Remaining + 1);
    }

    [Fact]
    public async Task AnExhaustedKey_ReadsZero_NeverBelow() {
        (SlidingWindowRateLimiter limiter, _) = Create(limit: 2);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await limiter.TryAcquireAsync("x", 2, ct);
        await limiter.TryAcquireAsync("x", 1, ct);

        Assert.Equal(0, (await limiter.PeekAsync("x", ct)).Remaining);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? key) {
        (SlidingWindowRateLimiter limiter, _) = Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => limiter.PeekAsync(key!, TestContext.Current.CancellationToken).AsTask());
    }
}
