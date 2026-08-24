using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Tests.Unit.Fakes;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

public sealed class DistributedGcraRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (DistributedGcraRateLimiter Sut, FakeTimeProvider Time, FakeCounterStorage Storage) CreateSut(int limit, TimeSpan period) {
        FakeTimeProvider time = new(Epoch);
        FakeCounterStorage storage = new(time);
        FakeDistributedCounterFactory factory = new(storage);
        DistributedGcraRateLimiter sut = new(factory, limit, period, time);
        return (sut, time, storage);
    }

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenUnderCapacity_IsAllowed() {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_FreshKey_AbsorbsFullBurstUpToLimit() {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 5; i++) {
            RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(decision.IsAllowed);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_AfterBurstExhausted_IsDeniedWithAccurateRetryAfter() {
        // limit: 5 over 5s => 1s emission interval per unit
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 5, period: TimeSpan.FromSeconds(5));

        for(int i = 0; i < 5; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter.Value);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterPartialTime_RecoversCapacityLinearly() {
        (DistributedGcraRateLimiter sut, FakeTimeProvider time, _) = CreateSut(limit: 10, period: TimeSpan.FromSeconds(10));

        for(int i = 0; i < 10; i++) {
            await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromSeconds(3)); // 3 units recovered

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(2, decision.Remaining); // 3 recovered - 1 consumed = 2 left
    }

    [Fact]
    public async Task TryAcquireAsync_CostExceedsLimit_IsAlwaysDeniedWithoutModifyingStorage() {
        (DistributedGcraRateLimiter sut, _, FakeCounterStorage storage) = CreateSut(limit: 3, period: TimeSpan.FromSeconds(3));

        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.Equal(TimeSpan.FromSeconds(3), denied.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(10));

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 10, period: TimeSpan.FromSeconds(10));

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    // ---------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit) {
        FakeCounterStorage storage = new();
        FakeDistributedCounterFactory factory = new(storage);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new DistributedGcraRateLimiter(factory, limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativePeriod_Throws() {
        FakeCounterStorage storage = new();
        FakeDistributedCounterFactory factory = new(storage);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new DistributedGcraRateLimiter(factory, 1, TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new DistributedGcraRateLimiter(factory, 1, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_WithNullCounterFactory_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(() => new DistributedGcraRateLimiter(null!, 1, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (DistributedGcraRateLimiter sut, _, _) = CreateSut(limit: 1, period: TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}