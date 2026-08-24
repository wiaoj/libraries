using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.LeakyBucket;

public sealed class LeakyBucketRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (LeakyBucketRateLimiter Sut, FakeTimeProvider Time) CreateSut(int capacity, TimeSpan period) {
        FakeTimeProvider time = new(Epoch);
        LeakyBucketRateLimiter sut = new(capacity, period, time);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderCapacity_IsAllowed() {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_EmptyBucket_AbsorbsFullBurstUpToCapacity() {
        // Same defining trait as TokenBucketRateLimiter/GcraRateLimiter: a fully-drained bucket
        // can take `capacity` requests instantly, back-to-back, with no waiting.
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterBurst_SustainedHighRateRequests_AreDenied() {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterPartialLeakTime_SomeCapacityIsAvailableAgain() {
        // capacity=10 over a 10s period => 1 unit/sec leak rate.
        (LeakyBucketRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 10, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // bucket now full (level=10)
        }

        time.Advance(TimeSpan.FromSeconds(3)); // ~3 units should have leaked away

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(2, decision.Remaining); // 3 leaked, 1 added by this request, 2 headroom left
    }

    [Fact]
    public async Task TryAcquireAsync_AfterFullPeriodElapsed_BucketReturnsToEmpty() {
        (LeakyBucketRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(5)); // a full period elapsed — bucket should be fully drained

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(10));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 10, period: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentRequests_NeverExceedsCapacity() {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 10, period: TimeSpan.FromSeconds(30));

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
    public async Task TryAcquireAsync_FullBucket_IsDenied_WithRetryAfterBasedOnOverflow() {
        // capacity=5 over 5s => 1 unit/sec. Fill it, then ask for 1 more with zero headroom left:
        // overflow=1 unit, leak rate=1/s => RetryAfter should be ~1s.
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter.Value);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_StillAppliesPartialLeak_ButWithholdsOnlyTheRequestedCost() {
        // A denied request must not "freeze" the bucket's drain progress — only its own cost is
        // withheld. Fill the bucket, advance partial leak time, get denied for a big cost, then
        // confirm the accrued leak is still there for a smaller request right after.
        (LeakyBucketRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 10, period: TimeSpan.FromSeconds(10)); // 1 unit/sec

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(3)); // 3 units leaked away

        RateLimitDecision deniedBigCost = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(deniedBigCost.IsAllowed);

        RateLimitDecision allowedSmallCost = await sut.TryAcquireAsync("key", cost: 2, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(allowedSmallCost.IsAllowed);
        Assert.Equal(1, allowedSmallCost.Remaining); // 3 leaked - 2 consumed = 1 headroom left
    }

    [Fact]
    public async Task TryAcquireAsync_CostGreaterThanCapacity_IsAlwaysDenied_RegardlessOfLeaking() {
        TimeSpan period = TimeSpan.FromSeconds(10);
        (LeakyBucketRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 5, period: period);

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(denied.IsAllowed);

        time.Advance(TimeSpan.FromDays(1)); // no amount of waiting helps — the bucket maxes out at capacity
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
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new LeakyBucketRateLimiter(capacity, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativePeriod_Throws() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new LeakyBucketRateLimiter(1, TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new LeakyBucketRateLimiter(1, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        // ArgumentException.ThrowIfNullOrEmpty deliberately throws the more specific
        // ArgumentNullException for null (not the base ArgumentException) — Assert.Throws<T>
        // requires an exact type match, so this has to be asserted separately from the empty case.
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (LeakyBucketRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}