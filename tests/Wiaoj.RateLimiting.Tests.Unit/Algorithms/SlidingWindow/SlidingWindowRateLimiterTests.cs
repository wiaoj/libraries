using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Tests.Unit.Fakes;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.SlidingWindow;

public sealed class SlidingWindowRateLimiterTests {
    // Aligned to a whole second so absolute-time window ids fall on clean boundaries —
    // makes the weighted-decay math in the tests easy to reason about.
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (SlidingWindowRateLimiter Sut, FakeTimeProvider Time) CreateSut(int limit, TimeSpan window) {
        FakeTimeProvider time = new(Epoch);
        FakeCounterStorage storage = new(time);
        FakeDistributedCounterFactory factory = new(storage);
        SlidingWindowRateLimiter sut = new(factory, limit, window, time);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_IsAllowed() {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 5, window: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AtExactLimit_IsStillAllowed_NoOffByOne() {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision third = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.IsAllowed);
        Assert.Equal(0, third.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(10));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 10, window: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AsTimeProgressesIntoNewWindow_PreviousWindowContributionDecays() {
        // window = 10s, limit = 10. Fill window 0 completely (10 requests).
        TimeSpan window = TimeSpan.FromSeconds(10);
        (SlidingWindowRateLimiter sut, FakeTimeProvider time) = CreateSut(limit: 10, window: window);

        for(int i = 0; i < 10; i++) {
            RateLimitDecision d = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(d.IsAllowed);
        }

        // Move to the very start of window 1: previous window is fully weighted (weight ≈ 1),
        // so estimated = 10*1 + 1(new) = 11 > 10 → denied.
        time.Advance(window);
        RateLimitDecision atWindowStart = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(atWindowStart.IsAllowed);

        // Move to the midpoint of window 1: previous window weight ≈ 0.5, so
        // estimated = 10*0.5 + 1(new) = 6 <= 10 → allowed. The rolled-back denial above must not
        // have permanently consumed window 1's capacity.
        time.Advance(TimeSpan.FromSeconds(5));
        RateLimitDecision atWindowMidpoint = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(atWindowMidpoint.IsAllowed);
    }

    // ---------------------------------------------------------------------
    // Negative cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_OverLimit_IsDenied() {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.Equal(0, denied.Remaining);
        Assert.NotNull(denied.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_RollsBackSpeculativeIncrement_DoesNotPermanentlyConsumeCapacity() {
        // If the rollback didn't work, the denied cost:5 attempt below would have permanently
        // inflated window 0's counter, making the subsequent cost:1 attempt denied too.
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 3, window: TimeSpan.FromSeconds(10));

        await sut.TryAcquireAsync("key", cost: 1, cancellationToken: TestContext.Current.CancellationToken); // used: 1/3

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5, cancellationToken: TestContext.Current.CancellationToken); // 1+5=6 > 3
        Assert.False(denied.IsAllowed);

        RateLimitDecision stillFits = await sut.TryAcquireAsync("key", cost: 2, cancellationToken: TestContext.Current.CancellationToken); // 1+2=3, exactly the limit
        Assert.True(stillFits.IsAllowed);
        Assert.Equal(0, stillFits.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_RetryAfter_NeverExceedsWindowDuration() {
        TimeSpan window = TimeSpan.FromSeconds(10);
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 1, window: window);

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.True(denied.RetryAfter.Value > TimeSpan.Zero);
        Assert.True(denied.RetryAfter.Value <= window);
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

        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new SlidingWindowRateLimiter(factory, limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativeWindow_Throws() {
        FakeTimeProvider time = new(Epoch);
        FakeCounterStorage storage = new(time);
        FakeDistributedCounterFactory factory = new(storage);

        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new SlidingWindowRateLimiter(factory, limit: 1, window: TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new SlidingWindowRateLimiter(factory, limit: 1, window: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_WithNullCounterFactory_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(
            () => new SlidingWindowRateLimiter(null!, limit: 1, window: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        // ArgumentException.ThrowIfNullOrEmpty deliberately throws the more specific
        // ArgumentNullException for null (not the base ArgumentException) — Assert.Throws<T>
        // requires an exact type match, so this has to be asserted separately from the empty case.
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (SlidingWindowRateLimiter sut, _) = CreateSut(limit: 1, window: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}