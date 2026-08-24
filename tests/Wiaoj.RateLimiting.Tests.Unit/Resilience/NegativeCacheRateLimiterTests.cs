using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Resilience;

namespace Wiaoj.RateLimiting.Tests.Unit.Resilience;

public sealed class NegativeCacheRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Spy algorithm used to count invocations to verify short-circuiting
    private sealed class SpyRateLimitAlgorithm : IRateLimitAlgorithm {
        public int InvocationCount { get; private set; }
        public Func<string, int, RateLimitDecision> DecisionFactory { get; set; } = static (_, _) => RateLimitDecision.Allowed(10);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            this.InvocationCount++;
            return ValueTask.FromResult(this.DecisionFactory(key, cost));
        }
    }

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_PassesThroughToInnerAndDoesNotCache() {
        FakeTimeProvider time = new(Epoch);
        SpyRateLimitAlgorithm spy = new() {
            DecisionFactory = static (_, _) => RateLimitDecision.Allowed(remaining: 5)
        };
        NegativeCacheRateLimiter sut = new(spy, time);

        RateLimitDecision decision = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(5, decision.Remaining);
        Assert.Equal(1, spy.InvocationCount);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenInnerDenies_CachesDenialAndShortCircuitsSubsequentRequests() {
        FakeTimeProvider time = new(Epoch);
        TimeSpan retryAfter = TimeSpan.FromSeconds(5);
        SpyRateLimitAlgorithm spy = new() {
            DecisionFactory = (_, _) => RateLimitDecision.Denied(retryAfter, remaining: 0)
        };
        NegativeCacheRateLimiter sut = new(spy, time);

        // 1st request: Hits inner algorithm and gets denied
        RateLimitDecision first = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(first.IsAllowed);
        Assert.Equal(retryAfter, first.RetryAfter);
        Assert.Equal(1, spy.InvocationCount);

        // 2nd and 3rd requests (1 second later): Should short-circuit from RAM (inner invocation count stays 1!)
        time.Advance(TimeSpan.FromSeconds(1));
        RateLimitDecision second = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision third = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(second.IsAllowed);
        Assert.False(third.IsAllowed);
        Assert.Equal(TimeSpan.FromSeconds(4), second.RetryAfter); // 5s - 1s elapsed = 4s remaining
        Assert.Equal(TimeSpan.FromSeconds(4), third.RetryAfter);
        Assert.Equal(1, spy.InvocationCount); // Inner was NOT called again!
    }

    [Fact]
    public async Task TryAcquireAsync_AfterRetryAfterExpires_EvictsFromCacheAndCallsInnerAgain() {
        FakeTimeProvider time = new(Epoch);
        TimeSpan retryAfter = TimeSpan.FromSeconds(3);
        bool shouldAllow = false;

        SpyRateLimitAlgorithm spy = new() {
            DecisionFactory = (_, _) => shouldAllow ? RateLimitDecision.Allowed(5) : RateLimitDecision.Denied(retryAfter, remaining: 0)
        };
        NegativeCacheRateLimiter sut = new(spy, time);

        // First call: Denied and cached for 3s
        await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, spy.InvocationCount);

        // Advance 3.1 seconds (denial cache entry expired)
        time.Advance(TimeSpan.FromSeconds(3.1));
        shouldAllow = true;

        // Next call: Calls inner again
        RateLimitDecision result = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal(2, spy.InvocationCount); // Inner was called!
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_DoNotBlockEachOtherInNegativeCache() {
        FakeTimeProvider time = new(Epoch);
        SpyRateLimitAlgorithm spy = new() {
            DecisionFactory = (key, _) => key == "blocked"
                ? RateLimitDecision.Denied(TimeSpan.FromSeconds(5))
                : RateLimitDecision.Allowed(10)
        };
        NegativeCacheRateLimiter sut = new(spy, time);

        await sut.TryAcquireAsync("blocked", cancellationToken: TestContext.Current.CancellationToken);
        RateLimitDecision cleanKey = await sut.TryAcquireAsync("allowed_key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(cleanKey.IsAllowed);
        Assert.Equal(2, spy.InvocationCount);
    }

    [Fact]
    public void Reset_ClearsAllTrackedNegativeCacheState() {
        FakeTimeProvider time = new(Epoch);
        SpyRateLimitAlgorithm spy = new() {
            DecisionFactory = static (_, _) => RateLimitDecision.Denied(TimeSpan.FromSeconds(10))
        };
        NegativeCacheRateLimiter sut = new(spy, time);

        _ = sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, spy.InvocationCount);

        sut.Reset(); // Clear cache

        _ = sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, spy.InvocationCount); // Hit inner again because cache was cleared
    }

    [Fact]
    public void Constructor_WithNullInner_ThrowsArgumentNullException() {
        Assert.ThrowsAny<ArgumentNullException>(() => new NegativeCacheRateLimiter(null!));
    }
}