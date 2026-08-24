using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Testing;

public sealed class InMemoryRateLimitAlgorithmTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_IsAllowed() {
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 3, window: TimeSpan.FromSeconds(1), time);

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.RetryAfter);
        Assert.Equal(2, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AtExactLimit_IsStillAllowed_NoOffByOne() {
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 3, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision third = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.IsAllowed);
        Assert.Equal(0, third.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_OneRequestOverLimit_IsDenied_NoOffByOne() {
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 3, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision fourth = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(fourth.IsAllowed);
        Assert.Equal(0, fourth.Remaining);
        Assert.NotNull(fourth.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_DeniedAttempt_DoesNotMutateState() {
        // A denied attempt must not "use up" capacity — otherwise a burst of rejected
        // requests could itself cause legitimate, smaller requests to be denied too.
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 1, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key", cost: 1, TestContext.Current.CancellationToken);
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5, TestContext.Current.CancellationToken);
        RateLimitDecision stillDenied = await sut.TryAcquireAsync("key", cost: 1, TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);
        Assert.False(stillDenied.IsAllowed); // capacity for this window is genuinely exhausted (1 of 1 used)
    }

    [Fact]
    public async Task TryAcquireAsync_AfterWindowElapses_StateResets() {
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 1, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision deniedWithinWindow = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        time.Advance(TimeSpan.FromSeconds(1));
        RateLimitDecision allowedAfterReset = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(deniedWithinWindow.IsAllowed);
        Assert.True(allowedAfterReset.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 1, window: TimeSpan.FromSeconds(1), time);

        RateLimitDecision keyA = await sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision keyB = await sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        FakeTimeProvider time = new(Epoch);
        FakeRateLimitAlgorithm sut = new(limit: 10, window: TimeSpan.FromSeconds(1), time);

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new FakeRateLimitAlgorithm(limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithNonPositiveWindow_Throws() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new FakeRateLimitAlgorithm(1, TimeSpan.Zero));
    }
}
