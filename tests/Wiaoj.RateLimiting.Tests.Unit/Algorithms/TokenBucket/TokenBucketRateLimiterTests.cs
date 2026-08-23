using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.TokenBucket;

public sealed class TokenBucketRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (TokenBucketRateLimiter Sut, FakeTimeProvider Time) CreateSut(int capacity, TimeSpan window) {
        FakeTimeProvider time = new(Epoch);
        TokenBucketRateLimiter sut = new(capacity, window, time);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderCapacity_IsAllowed() {
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 5, window: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_FreshBucket_AbsorbsFullBurstUpToCapacity() {
        // The defining trait of token bucket vs fixed/sliding window: a fully-idle bucket can take
        // `capacity` requests instantly, back-to-back, with no waiting.
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 5, window: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterBurst_SustainedHighRateRequests_AreDenied() {
        // Proves the "burst-tolerant but not unlimited" trade-off the README calls out: exhaust
        // the bucket, then hammer it with no time passing — everything past capacity is denied.
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 5, window: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterPartialRefillTime_SomeCapacityIsAvailableAgain() {
        // capacity=10 over a 10s window => 1 token/sec refill rate.
        (TokenBucketRateLimiter? sut, FakeTimeProvider time) = CreateSut(capacity: 10, window: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // bucket now empty
        }

        time.Advance(TimeSpan.FromSeconds(3)); // ~3 tokens should have refilled

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(2, decision.Remaining); // 3 refilled, 1 consumed by this request, 2 left
    }

    [Fact]
    public async Task TryAcquireAsync_AfterFullRefillWindow_BucketReturnsToFullCapacity() {
        (TokenBucketRateLimiter? sut, FakeTimeProvider time) = CreateSut(capacity: 5, window: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(5)); // a full window elapsed — bucket should be full again

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 1, window: TimeSpan.FromSeconds(10));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleTokens() {
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 10, window: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentRequests_NeverExceedsCapacity() {
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 10, window: TimeSpan.FromSeconds(30));

        Task<RateLimitDecision>[] tasks = new Task<RateLimitDecision>[50];
        for(int i = 0; i < tasks.Length; i++) {
            tasks[i] = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken).AsTask();
        }
        RateLimitDecision[] results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Count(r => r.IsAllowed));
    }

    // ---------------------------------------------------------------------
    // Negative cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_EmptyBucket_IsDenied_WithRetryAfterBasedOnDeficit() {
        // capacity=5 over 5s => 1 token/sec. Drain it, then ask for 1 more with 0 tokens left:
        // deficit=1 token, refill rate=1/s => RetryAfter should be ~1s.
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 5, window: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter.Value);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_StillAppliesPartialRefill_ButWithholdsOnlyTheRequestedCost() {
        // A denied request must not "freeze" the bucket's refill progress — only its own cost is
        // withheld. Drain the bucket, advance partial refill time, get denied for a big cost, then
        // confirm the accrued refill is still there for a smaller request right after.
        (TokenBucketRateLimiter? sut, FakeTimeProvider time) = CreateSut(capacity: 10, window: TimeSpan.FromSeconds(10)); // 1 token/sec

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(3)); // 3 tokens refilled

        RateLimitDecision deniedBigCost = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(deniedBigCost.IsAllowed);

        RateLimitDecision allowedSmallCost = await sut.TryAcquireAsync("key", cost: 2, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(allowedSmallCost.IsAllowed);
        Assert.Equal(1, allowedSmallCost.Remaining); // 3 refilled - 2 consumed = 1
    }

    [Fact]
    public async Task TryAcquireAsync_CostGreaterThanCapacity_IsAlwaysDenied_RegardlessOfRefill() {
        TimeSpan window = TimeSpan.FromSeconds(10);
        (TokenBucketRateLimiter? sut, FakeTimeProvider time) = CreateSut(capacity: 5, window: window);

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(denied.IsAllowed);

        time.Advance(TimeSpan.FromDays(1)); // no amount of waiting helps — bucket maxes out at capacity
        RateLimitDecision stillDenied = await sut.TryAcquireAsync("key", cost: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(stillDenied.IsAllowed);
    }

    // ---------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCapacity_Throws(int capacity) {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenBucketRateLimiter(capacity, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativeWindow_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TokenBucketRateLimiter(1, TimeSpan.Zero));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        // ArgumentException.ThrowIfNullOrEmpty deliberately throws the more specific
        // ArgumentNullException for null (not the base ArgumentException) — Assert.Throws<T>
        // requires an exact type match, so this has to be asserted separately from the empty case.
        (TokenBucketRateLimiter sut, _) = CreateSut(capacity: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (TokenBucketRateLimiter sut, _) = CreateSut(capacity: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (TokenBucketRateLimiter? sut, _) = CreateSut(capacity: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}