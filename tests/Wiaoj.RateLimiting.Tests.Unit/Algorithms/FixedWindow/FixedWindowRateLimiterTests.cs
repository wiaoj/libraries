using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Tests.Unit.Fakes;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.FixedWindow;

public sealed class FixedWindowRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (FixedWindowRateLimiter Sut, FakeTimeProvider Time) CreateSut(int limit, TimeSpan window) {
        FakeTimeProvider time = new(Epoch);
        FakeCounterStorage storage = new(time);
        FakeDistributedCounterFactory factory = new(storage);
        FixedWindowRateLimiter sut = new(factory, limit, window);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_IsAllowed() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(1));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.RetryAfter);
        Assert.Equal(2, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AtExactLimit_IsStillAllowed_NoOffByOne() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(1));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision third = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.IsAllowed);
        Assert.Equal(0, third.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_SingleCallEqualToLimit_IsAllowed() {
        // Boundary distinct from "AtExactLimit": here the *entire* limit is consumed by one call
        // (cost == limit) rather than accumulated one unit at a time.
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 5, window: TimeSpan.FromSeconds(1));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(0, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 10, window: TimeSpan.FromSeconds(1));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterWindowElapses_StateResets() {
        (FixedWindowRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision deniedWithinWindow = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromSeconds(1));
        RateLimitDecision allowedAfterReset = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(deniedWithinWindow.IsAllowed);
        Assert.True(allowedAfterReset.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_AcrossMultipleWindowCycles_ResetsEachTime() {
        // A single reset isn't proof the window logic is correct on an ongoing basis — verify it
        // holds across several consecutive cycles, not just the first one.
        (FixedWindowRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 2, window: TimeSpan.FromSeconds(1));

        for(int cycle = 0; cycle < 5; cycle++) {
            RateLimitDecision first = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            RateLimitDecision second = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            RateLimitDecision third = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(first.IsAllowed);
            Assert.True(second.IsAllowed);
            Assert.False(third.IsAllowed);

            time.Advance(TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentRequests_NeverExceedsLimit() {
        // The whole point of routing through IDistributedCounter.TryIncrementAsync instead of a
        // read-then-write in this class is atomicity under concurrency. Fire more requests than
        // the limit allows, in parallel, and verify at most `limit` of them were allowed.
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 10, window: TimeSpan.FromSeconds(5));

        Task<RateLimitDecision>[] tasks = new Task<RateLimitDecision>[50];
        for(int i = 0; i < tasks.Length; i++) {
            tasks[i] = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken).AsTask();
        }
        RateLimitDecision[] results = await Task.WhenAll(tasks);

        int allowedCount = results.Count(r => r.IsAllowed);
        Assert.Equal(10, allowedCount);
    }

    // ---------------------------------------------------------------------
    // Negative cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_OneRequestOverLimit_IsDenied_NoOffByOne() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(1));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision fourth = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(fourth.IsAllowed);
        Assert.Equal(0, fourth.Remaining);
        Assert.NotNull(fourth.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_CostGreaterThanRemainingCapacity_IsDenied_AndDoesNotPartiallyConsume() {
        // A request whose cost doesn't fit should be denied outright, not partially applied.
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 5, window: TimeSpan.FromSeconds(1));

        await sut.TryAcquireAsync("key", cost: 3, cancellationToken: TestContext.Current.CancellationToken); // 3 used, 2 remaining
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 3, cancellationToken: TestContext.Current.CancellationToken); // doesn't fit in remaining 2
        RateLimitDecision stillFits = await sut.TryAcquireAsync("key", cost: 2, cancellationToken: TestContext.Current.CancellationToken); // exactly the 2 that remained

        Assert.False(denied.IsAllowed);
        Assert.True(stillFits.IsAllowed);
        Assert.Equal(0, stillFits.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_CostExceedsLimit_OnFirstEverCallForKey_IsDenied_AndRetryAfterFallsBackToFullWindow() {
        // No window has ever been established for this key (the very first increment is the one
        // that gets rejected), so there's no TTL to read back — RetryAfter must conservatively
        // fall back to the full window rather than claiming "0s until you can retry".
        TimeSpan window = TimeSpan.FromSeconds(10);
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 3, window: window);

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.Equal(window, denied.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenDenied_RetryAfterReflectsActualRemainingTtl_NotFullWindow() {
        (FixedWindowRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // consumes the only slot, starts the window

        time.Advance(TimeSpan.FromSeconds(9)); // 9s into a 10s window — 1s should remain

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.True(denied.RetryAfter.Value <= TimeSpan.FromSeconds(1));
        Assert.True(denied.RetryAfter.Value > TimeSpan.Zero);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_DoesNotMutateState() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await sut.TryAcquireAsync("key", cost: 1, cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision stillDenied = await sut.TryAcquireAsync("key", cost: 1, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.False(stillDenied.IsAllowed);
    }

    // ---------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit) {
        FakeTimeProvider time = new(Epoch);
        FakeCounterStorage storage = new(time);
        FakeDistributedCounterFactory factory = new(storage);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedWindowRateLimiter(factory, limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativeWindow_Throws() {
        FakeTimeProvider time = new(Epoch);
        FakeCounterStorage storage = new(time);
        FakeDistributedCounterFactory factory = new(storage);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedWindowRateLimiter(factory, limit: 1, window: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedWindowRateLimiter(factory, limit: 1, window: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_WithNullCounterFactory_Throws() {
        Assert.Throws<ArgumentNullException>(
            () => new FixedWindowRateLimiter(null!, limit: 1, window: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        // ArgumentException.ThrowIfNullOrEmpty deliberately throws the more specific
        // ArgumentNullException for null (not the base ArgumentException) — Assert.Throws<T>
        // requires an exact type match, so this has to be asserted separately from the empty case.
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (FixedWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}