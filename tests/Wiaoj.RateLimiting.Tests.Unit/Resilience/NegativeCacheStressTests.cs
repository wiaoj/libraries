using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
[Trait("Feature", "NegativeCacheStress")]
public sealed class NegativeCacheStressTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public sealed class TheColdStartStampede {

        [Fact]
        public async Task ColdStart_100ParallelRequestsOnEmptyCache_ThreadSafelyCachesAndDeflectsSubsequentStorm() {
            // Arrange: 100 parallel requests arriving at the exact same millisecond on an empty cache
            FakeTimeProvider time = new(Epoch);
            AtomicCountingAlgorithm inner = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(5), remaining: 0));
            NegativeCacheRateLimiter sut = new(inner, time);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: 100 simultaneous requests on cold cache
            Task<RateLimitDecision>[] coldTasks = [.. Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() => sut.TryAcquireAsync("cold_spammer", 1, ct).AsTask()))];

            RateLimitDecision[] coldResults = await Task.WhenAll(coldTasks);

            // Assert 1: All requests must be denied
            Assert.All(coldResults, static r => Assert.False(r.IsAllowed));

            int innerCallsAfterColdStorm = inner.CallCount;

            // Advance 1 second into cooldown
            time.Advance(TimeSpan.FromSeconds(1));

            // Act 2: 100 more requests during the active cooldown
            Task<RateLimitDecision>[] subsequentTasks = [.. Enumerable.Range(0, 100)
                .Select(_ => Task.Run(() => sut.TryAcquireAsync("cold_spammer", 1, ct).AsTask()))];

            RateLimitDecision[] subsequentResults = await Task.WhenAll(subsequentTasks);

            // Assert 2: ZERO calls reached the inner store during the second wave!
            Assert.All(subsequentResults, static r => Assert.False(r.IsAllowed));
            Assert.Equal(innerCallsAfterColdStorm, inner.CallCount);
        }
    }

    public sealed class TheExpirationSeamRace {

        [Fact]
        public async Task ExpirationBoundary_ParallelRequestsAtExactExpirationMoment_CleanlyRefreshesCache() {
            // Arrange
            FakeTimeProvider time = new(Epoch);
            AtomicCountingAlgorithm inner = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(3), remaining: 0));
            NegativeCacheRateLimiter sut = new(inner, time);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // 1. Initial ban for 3 seconds
            await sut.TryAcquireAsync("seam_spammer", 1, ct);
            Assert.Equal(1, inner.CallCount);

            // 2. Fast forward time past the 3-second penalty
            time.Advance(TimeSpan.FromSeconds(4));

            // 3. 50 parallel requests arriving immediately as the ban expires
            Task<RateLimitDecision>[] tasks = [.. Enumerable.Range(0, 50)
                .Select(_ => Task.Run(() => sut.TryAcquireAsync("seam_spammer", 1, ct).AsTask()))];

            RateLimitDecision[] results = await Task.WhenAll(tasks);

            // Assert: All denied again (re-banned), and subsequent calls immediately short-circuit
            Assert.All(results, static r => Assert.False(r.IsAllowed));

            int callsAfterRefresh = inner.CallCount;

            // Another request 1s later must hit the new cache
            time.Advance(TimeSpan.FromSeconds(1));
            RateLimitDecision cachedDecision = await sut.TryAcquireAsync("seam_spammer", 1, ct);

            Assert.False(cachedDecision.IsAllowed);
            Assert.Equal(callsAfterRefresh, inner.CallCount);
        }
    }

    public sealed class TheMultiKeyHighContention {

        [Fact]
        public async Task MixedTraffic_BlockedSpammersAndLegitimateUsers_OperateConcurrentlyWithoutCrossContamination() {
            // Arrange: 5 spammer IPs (denied) and 5 legitimate IPs (allowed)
            FakeTimeProvider time = new(Epoch);
            ConfigurableOutcomeAlgorithm inner = new();
            NegativeCacheRateLimiter sut = new(inner, time);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Spammers are denied for 10 seconds
            for(int s = 1; s <= 5; s++) {
                inner.SetOutcome($"spammer_{s}", RateLimitDecision.Denied(TimeSpan.FromSeconds(10), remaining: 0));
            }

            // Legitimate users are allowed
            for(int u = 1; u <= 5; u++) {
                inner.SetOutcome($"user_{u}", RateLimitDecision.Allowed(remaining: 100));
            }

            const int requestsPerKey = 20;

            // Act: 200 concurrent tasks hammering all 10 keys simultaneously
            List<Task<(string Key, RateLimitDecision Decision)>> allTasks = [];

            for(int i = 0; i < requestsPerKey; i++) {
                for(int s = 1; s <= 5; s++) {
                    string spammerKey = $"spammer_{s}";
                    allTasks.Add(Task.Run(async () => (spammerKey, await sut.TryAcquireAsync(spammerKey, 1, ct))));
                }

                for(int u = 1; u <= 5; u++) {
                    string userKey = $"user_{u}";
                    allTasks.Add(Task.Run(async () => (userKey, await sut.TryAcquireAsync(userKey, 1, ct))));
                }
            }

            (string Key, RateLimitDecision Decision)[] results = await Task.WhenAll(allTasks);

            // Assert 1: All spammer requests must be denied
            Assert.All(results.Where(r => r.Key.StartsWith("spammer_")), static r => Assert.False(r.Decision.IsAllowed));

            // Assert 2: All legitimate user requests must be allowed
            Assert.All(results.Where(r => r.Key.StartsWith("user_")), static r => Assert.True(r.Decision.IsAllowed));

            // Assert 3: Each spammer reached inner store only on initial check (rest were short-circuited in RAM)
            for(int s = 1; s <= 5; s++) {
                Assert.True(inner.GetKeyCallCount($"spammer_{s}") < requestsPerKey);
            }

            // Assert 4: Legitimate users passed through every time (20 calls each)
            for(int u = 1; u <= 5; u++) {
                Assert.Equal(requestsPerKey, inner.GetKeyCallCount($"user_{u}"));
            }
        }
    }

    private sealed class AtomicCountingAlgorithm(RateLimitDecision outcome) : IRateLimitAlgorithm {
        private int _callCount;
        public int CallCount => Volatile.Read(ref this._callCount);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._callCount);
            return ValueTask.FromResult(outcome);
        }
    }

    private sealed class ConfigurableOutcomeAlgorithm : IRateLimitAlgorithm {
        private readonly Dictionary<string, RateLimitDecision> _outcomes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _callCounts = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public void SetOutcome(string key, RateLimitDecision outcome) {
            lock(this._gate) {
                this._outcomes[key] = outcome;
            }
        }

        public int GetKeyCallCount(string key) {
            lock(this._gate) {
                return this._callCounts.TryGetValue(key, out int count) ? count : 0;
            }
        }

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default) {
            lock(this._gate) {
                this._callCounts[key] = (this._callCounts.TryGetValue(key, out int count) ? count : 0) + 1;
                RateLimitDecision outcome = this._outcomes.TryGetValue(key, out RateLimitDecision d) ? d : RateLimitDecision.Allowed();
                return ValueTask.FromResult(outcome);
            }
        }
    }
}