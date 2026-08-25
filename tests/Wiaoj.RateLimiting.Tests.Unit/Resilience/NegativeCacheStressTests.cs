using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Resilience;

public sealed class NegativeCacheStressTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class AtomicCountingAlgorithm : IRateLimitAlgorithm {
        private int _callCount;
        public int CallCount => Volatile.Read(ref this._callCount);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._callCount);
            // Always return denied with 5s cooldown
            return ValueTask.FromResult(RateLimitDecision.Denied(TimeSpan.FromSeconds(5), remaining: 0));
        }
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentSpamRequests_ShortCircuitsAllParallelCalls() {
        FakeTimeProvider time = new(Epoch);
        AtomicCountingAlgorithm inner = new();
        NegativeCacheRateLimiter sut = new(inner, time);

        // 1st call: Populates negative cache
        await sut.TryAcquireAsync("spammer_ip", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, inner.CallCount);

        // Advance 1 second (still in 5s penalty window)
        time.Advance(TimeSpan.FromSeconds(1));

        // 50 parallel requests hitting the same key
        Task<RateLimitDecision>[] tasks = new Task<RateLimitDecision>[50];
        for(int i = 0; i < tasks.Length; i++) {
            tasks[i] = sut.TryAcquireAsync("spammer_ip", cancellationToken: TestContext.Current.CancellationToken).AsTask();
        }

        RateLimitDecision[] results = await Task.WhenAll(tasks);

        // All 50 requests must be denied
        Assert.All(results, r => Assert.False(r.IsAllowed));
        Assert.All(results, r => Assert.Equal(TimeSpan.FromSeconds(4), r.RetryAfter));

        // CRITICAL ASSERTION: Inner storage was NEVER called during the spam storm!
        Assert.Equal(1, inner.CallCount);
    }
}