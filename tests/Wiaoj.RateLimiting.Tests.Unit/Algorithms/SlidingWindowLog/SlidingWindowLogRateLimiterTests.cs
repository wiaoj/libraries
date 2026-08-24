using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.SlidingWindowLog;

public sealed class SlidingWindowLogRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (SlidingWindowLogRateLimiter Sut, FakeTimeProvider Time) CreateSut(int limit, TimeSpan window) {
        FakeTimeProvider time = new(Epoch);
        SlidingWindowLogRateLimiter sut = new(limit, window, time);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_IsAllowed() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 5, window: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AtExactLimit_IsStillAllowed_NoOffByOne() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision third = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.IsAllowed);
        Assert.Equal(0, third.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(10));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 10, window: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_EntriesExpireIndividually_NotAllAtOnce_UnlikeFixedWindow() {
        // The defining trait vs both FixedWindowRateLimiter and the weighted SlidingWindowRateLimiter:
        // capacity trickles back in exactly `window` after each individual entry, not in one lump
        // reset. limit=3, window=10s. Record one request every 3s.
        TimeSpan window = TimeSpan.FromSeconds(10);
        (SlidingWindowLogRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 3, window: window);

        RateLimitDecision atT0 = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // t=0
        time.Advance(TimeSpan.FromSeconds(3));
        RateLimitDecision atT3 = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // t=3
        time.Advance(TimeSpan.FromSeconds(3));
        RateLimitDecision atT6 = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // t=6

        Assert.True(atT0.IsAllowed);
        Assert.True(atT3.IsAllowed);
        Assert.True(atT6.IsAllowed);

        // t=9: entries at 0, 3, 6 are all still within the last 10s -> 3 + 1 = 4 > 3 -> denied.
        time.Advance(TimeSpan.FromSeconds(3));
        RateLimitDecision atT9 = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(atT9.IsAllowed);
        Assert.Equal(TimeSpan.FromSeconds(1), atT9.RetryAfter); // the t=0 entry ages out at t=10, 1s away

        // t=10.1: only the t=0 entry has aged out (its window closed at t=10). t=3 and t=6 remain
        // (cost 2) + this new request (cost 1) = 3 <= 3 -> allowed. The denial above must not have
        // consumed capacity, and the still-live t=3/t=6 entries must not have been evicted early.
        time.Advance(TimeSpan.FromMilliseconds(1100));
        RateLimitDecision atT10_1 = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(atT10_1.IsAllowed);
        Assert.Equal(0, atT10_1.Remaining);
    }

    // ---------------------------------------------------------------------
    // Negative cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_OverLimit_IsDenied() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.Equal(0, denied.Remaining);
        Assert.NotNull(denied.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_DoesNotConsumeCapacity() {
        // A denied cost:5 attempt must never be appended to the log — otherwise it would
        // permanently occupy capacity a smaller, later request could legitimately have used.
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cost: 1, cancellationToken: TestContext.Current.CancellationToken); // used: 1/3

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken); // 1+5=6 > 3
        Assert.False(denied.IsAllowed);

        RateLimitDecision stillFits = await sut.TryAcquireAsync("key", cost: 2, cancellationToken: TestContext.Current.CancellationToken); // 1+2=3, exactly the limit
        Assert.True(stillFits.IsAllowed);
        Assert.Equal(0, stillFits.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_CostExceedsLimit_OnFirstEverCallForKey_IsDenied_AndRetryAfterFallsBackToFullWindow() {
        // No log entries exist yet for this key, so there's no "oldest entry" to measure an
        // expiry against — RetryAfter must conservatively fall back to the full window.
        TimeSpan window = TimeSpan.FromSeconds(10);
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 3, window: window);

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.Equal(window, denied.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_RetryAfter_ReflectsOldestEntryExpiry_NotFullWindow() {
        TimeSpan window = TimeSpan.FromSeconds(10);
        (SlidingWindowLogRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 1, window: window);

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // consumes the only slot at t=0

        time.Advance(TimeSpan.FromSeconds(9)); // 9s into a 10s window — 1s should remain

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentRequests_NeverExceedsLimit() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 10, window: TimeSpan.FromSeconds(30));

        Task<RateLimitDecision>[] tasks = new Task<RateLimitDecision>[50];
        for(int i = 0; i < tasks.Length; i++) {
            tasks[i] = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken).AsTask();
        }
        RateLimitDecision[] results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Count(r => r.IsAllowed));
    }

    // ---------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new SlidingWindowLogRateLimiter(limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativeWindow_Throws() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new SlidingWindowLogRateLimiter(1, TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new SlidingWindowLogRateLimiter(1, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        // ArgumentException.ThrowIfNullOrEmpty deliberately throws the more specific
        // ArgumentNullException for null (not the base ArgumentException) — Assert.Throws<T>
        // requires an exact type match, so this has to be asserted separately from the empty case.
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (SlidingWindowLogRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}