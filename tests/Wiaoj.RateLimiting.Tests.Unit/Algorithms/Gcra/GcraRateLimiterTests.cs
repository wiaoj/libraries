using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.Gcra;

public sealed class GcraRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (GcraRateLimiter Sut, FakeTimeProvider Time) CreateSut(int limit, TimeSpan period) {
        FakeTimeProvider time = new(Epoch);
        GcraRateLimiter sut = new(limit, period, time);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderCapacity_IsAllowed() {
        (GcraRateLimiter sut, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_FreshKey_AbsorbsFullBurstUpToLimit() {
        // Same defining trait as TokenBucketRateLimiter: a fully-idle key can take `limit`
        // requests instantly, back-to-back, with no waiting — GCRA is mathematically equivalent.
        (GcraRateLimiter sut, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterBurst_SustainedHighRateRequests_AreDenied() {
        (GcraRateLimiter sut, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterPartialEmissionTime_SomeCapacityIsAvailableAgain() {
        // limit=10 over a 10s period => 1 unit/sec emission rate.
        (GcraRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 10, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // fully drained
        }

        time.Advance(TimeSpan.FromSeconds(3)); // ~3 units should have "emitted" back

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(2, decision.Remaining); // 3 earned back, 1 consumed by this request, 2 left
    }

    [Fact]
    public async Task TryAcquireAsync_AfterFullPeriodElapsed_ReturnsToFullCapacity() {
        (GcraRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(5)); // a full period elapsed — back to full capacity

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (GcraRateLimiter sut, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(10));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        (GcraRateLimiter sut, _) = CreateSut(limit: 10, period: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentRequests_NeverExceedsLimit() {
        (GcraRateLimiter sut, _) = CreateSut(limit: 10, period: TimeSpan.FromSeconds(30));

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
    public async Task TryAcquireAsync_DrainedKey_IsDenied_WithRetryAfterBasedOnDeficit() {
        // limit=5 over 5s => 1 unit/sec. Drain it, then ask for 1 more with zero headroom left:
        // deficit=1 unit, emission rate=1/s => RetryAfter should be ~1s.
        (GcraRateLimiter sut, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter.Value);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_LeavesProjectionUntouched_SmallerRequestRightAfterStillFits() {
        // A denied request must not advance the stored TAT — only its own cost is withheld, the
        // partial "emission credit" already earned since the last accepted request must still be
        // there for a smaller request right after.
        (GcraRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 10, period: TimeSpan.FromSeconds(10)); // 1 unit/sec

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(3)); // 3 units' worth of headroom earned back

        RateLimitDecision deniedBigCost = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(deniedBigCost.IsAllowed);

        RateLimitDecision allowedSmallCost = await sut.TryAcquireAsync("key", cost: 2, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(allowedSmallCost.IsAllowed);
        Assert.Equal(1, allowedSmallCost.Remaining); // 3 earned back - 2 consumed = 1
    }

    [Fact]
    public async Task TryAcquireAsync_CostGreaterThanLimit_IsAlwaysDenied_RegardlessOfWaiting() {
        TimeSpan period = TimeSpan.FromSeconds(10);
        (GcraRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 5, period: period);

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(denied.IsAllowed);

        time.Advance(TimeSpan.FromDays(1)); // no amount of waiting helps — a fully-idle key still maxes out at `limit`
        RateLimitDecision stillDenied = await sut.TryAcquireAsync("key", cost: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(stillDenied.IsAllowed);
    }

    // ---------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new GcraRateLimiter(limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativePeriod_Throws() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new GcraRateLimiter(1, TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new GcraRateLimiter(1, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        // ArgumentException.ThrowIfNullOrEmpty deliberately throws the more specific
        // ArgumentNullException for null (not the base ArgumentException) — Assert.Throws<T>
        // requires an exact type match, so this has to be asserted separately from the empty case.
        (GcraRateLimiter sut, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (GcraRateLimiter sut, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (GcraRateLimiter sut, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}