using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Testing;

public sealed class InMemoryRateLimitAlgorithmTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_IsAllowed() {
        FakeTimeProvider time = new(Epoch);
        InMemoryRateLimitAlgorithm sut = new(limit: 3, window: TimeSpan.FromSeconds(1), time);

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.RetryAfter);
        Assert.Equal(2, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_AtExactLimit_IsStillAllowed_NoOffByOne() {
        FakeTimeProvider time = new(Epoch);
        InMemoryRateLimitAlgorithm sut = new(limit: 3, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key");
        await sut.TryAcquireAsync("key");
        RateLimitDecision third = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.IsAllowed);
        Assert.Equal(0, third.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_OneRequestOverLimit_IsDenied_NoOffByOne() {
        FakeTimeProvider time = new(Epoch);
        InMemoryRateLimitAlgorithm sut = new(limit: 3, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key");
        await sut.TryAcquireAsync("key");
        await sut.TryAcquireAsync("key");
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
        InMemoryRateLimitAlgorithm sut = new(limit: 1, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key", cost: 1);
        RateLimitDecision denied = await sut.TryAcquireAsync("key", cost: 5);
        RateLimitDecision stillDenied = await sut.TryAcquireAsync("key", cost: 1);

        Assert.False(denied.IsAllowed);
        Assert.False(stillDenied.IsAllowed); // capacity for this window is genuinely exhausted (1 of 1 used)
    }

    [Fact]
    public async Task TryAcquireAsync_AfterWindowElapses_StateResets() {
        FakeTimeProvider time = new(Epoch);
        InMemoryRateLimitAlgorithm sut = new(limit: 1, window: TimeSpan.FromSeconds(1), time);

        await sut.TryAcquireAsync("key");
        RateLimitDecision deniedWithinWindow = await sut.TryAcquireAsync("key");

        time.Advance(TimeSpan.FromSeconds(1));
        RateLimitDecision allowedAfterReset = await sut.TryAcquireAsync("key");

        Assert.False(deniedWithinWindow.IsAllowed);
        Assert.True(allowedAfterReset.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotAffectEachOther() {
        FakeTimeProvider time = new(Epoch);
        InMemoryRateLimitAlgorithm sut = new(limit: 1, window: TimeSpan.FromSeconds(1), time);

        RateLimitDecision keyA = await sut.TryAcquireAsync("a");
        RateLimitDecision keyB = await sut.TryAcquireAsync("b");

        Assert.True(keyA.IsAllowed);
        Assert.True(keyB.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCost_ConsumesMultipleUnits() {
        FakeTimeProvider time = new(Epoch);
        InMemoryRateLimitAlgorithm sut = new(limit: 10, window: TimeSpan.FromSeconds(1), time);

        RateLimitDecision decision = await sut.TryAcquireAsync("key", cost: 4);

        Assert.True(decision.IsAllowed);
        Assert.Equal(6, decision.Remaining);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit) {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new InMemoryRateLimitAlgorithm(limit, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithNonPositiveWindow_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new InMemoryRateLimitAlgorithm(1, TimeSpan.Zero));
    }
}
