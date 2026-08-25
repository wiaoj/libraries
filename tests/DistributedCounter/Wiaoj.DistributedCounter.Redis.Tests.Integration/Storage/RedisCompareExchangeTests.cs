using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Storage;

[Collection(RedisTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "CompareExchange")]
public sealed class RedisCompareExchangeTests {
    [Collection(RedisTestCollection.Name)]
    public sealed class TheBasicCasOperations {
        private readonly RedisCounterStorage _storage;

        public TheBasicCasOperations(RedisTestFixture fixture) {
            this._storage = new RedisCounterStorage(fixture.Connection);
        }

        [Fact]
        public async Task TryCompareExchange_WhenValueMatches_UpdatesValueInRedisAndReturnsTrue() {
            // Arrange
            CounterKey key = new($"redis:cas:match:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;

            await this._storage.SetAsync(key, new CounterValue(100), CounterExpiry.Infinite, ct);

            // Act: Expect 100, replace with 250
            bool success = await this._storage.TryCompareExchangeAsync(
                key,
                expectedValue: 100,
                newValue: 250,
                CounterExpiry.Infinite,
                ct);

            // Assert
            Assert.True(success);
            CounterValue readBack = await this._storage.GetAsync(key, ct);
            Assert.Equal(250, readBack.Value);
        }

        [Fact]
        public async Task TryCompareExchange_WhenValueMismatches_LeavesRedisUntouchedAndReturnsFalse() {
            // Arrange
            CounterKey key = new($"redis:cas:mismatch:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;

            await this._storage.SetAsync(key, new CounterValue(100), CounterExpiry.Infinite, ct);

            // Act: Expect 999 (wrong), attempt update to 500
            bool success = await this._storage.TryCompareExchangeAsync(
                key,
                expectedValue: 999,
                newValue: 500,
                CounterExpiry.Infinite,
                ct);

            // Assert
            Assert.False(success);
            CounterValue readBack = await this._storage.GetAsync(key, ct);
            Assert.Equal(100, readBack.Value); // Value in Redis remains untouched
        }

        [Fact]
        public async Task TryCompareExchange_OnNonExistentKey_TreatsInitialValueAsZero() {
            // Arrange: Fresh key never touched in Redis before
            CounterKey key = new($"redis:cas:fresh:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Expect 0 on brand-new key, set to 42
            bool success = await this._storage.TryCompareExchangeAsync(
                key,
                expectedValue: 0,
                newValue: 42,
                CounterExpiry.Infinite,
                ct);

            // Assert
            Assert.True(success);
            CounterValue readBack = await this._storage.GetAsync(key, ct);
            Assert.Equal(42, readBack.Value);
        }

        [Fact]
        public async Task TryCompareExchange_WithExpiry_SetsRedisPttlCorrectly() {
            // Arrange
            CounterKey key = new($"redis:cas:ttl:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;
            CounterExpiry expiry = CounterExpiry.FromSeconds(30);

            await this._storage.SetAsync(key, new CounterValue(10), CounterExpiry.Infinite, ct);

            // Act: Successful CAS with a 30s TTL
            bool success = await this._storage.TryCompareExchangeAsync(
                key,
                expectedValue: 10,
                newValue: 20,
                expiry,
                ct);

            // Assert
            Assert.True(success);
            Assert.Equal(20, (await this._storage.GetAsync(key, ct)).Value);

            TimeSpan? ttl = await this._storage.GetTtlAsync(key, ct);
            Assert.NotNull(ttl);
            Assert.True(ttl.Value.TotalSeconds is > 25 and <= 30);
        }
    }
    
    [Collection(RedisTestCollection.Name)]
    public sealed class TheConcurrencyAndRaceConditions {
        private readonly RedisCounterStorage _storage;

        public TheConcurrencyAndRaceConditions(RedisTestFixture fixture) {
            this._storage = new RedisCounterStorage(fixture.Connection);
        }

        [Fact]
        public async Task ConcurrentCas_OnRealRedis_WhenMultipleTasksRaceToTransitionState_ExactlyOneWins() {
            // Arrange
            CounterKey key = new($"redis:cas:race:single:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;
            const int concurrency = 50;

            // Initial Redis state: 0 (e.g. State: Pending)
            await this._storage.SetAsync(key, CounterValue.Zero, CounterExpiry.Infinite, ct);

            int successfulTransitions = 0;

            // Act: 50 concurrent tasks on real Redis competing to transition state from 0 to 1
            Task[] tasks = [.. Enumerable.Range(0, concurrency)
                .Select(_ => Task.Run(async () => {
                    bool result = await this._storage.TryCompareExchangeAsync(
                        key,
                        expectedValue: CounterValue.Zero,
                        newValue: new CounterValue(1),
                        CounterExpiry.FromMinutes(1),
                        ct);

                    if (result) {
                        Interlocked.Increment(ref successfulTransitions);
                    }
                }, ct))];

            await Task.WhenAll(tasks);

            // Assert: Real Redis Lua script guaranteed exact atomicity (Exactly 1 winner, 49 losers)
            Assert.Equal(1, successfulTransitions);
            CounterValue finalVal = await this._storage.GetAsync(key, ct);
            Assert.Equal(1, finalVal.Value);
        }

        [Fact]
        public async Task ConcurrentCasLoop_OnRealRedis_PreservesExactTotalUnderHighContention() {
            // Arrange
            CounterKey key = new($"redis:cas:race:loop:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;

            await this._storage.SetAsync(key, CounterValue.Zero, CounterExpiry.FromMinutes(2), ct);

            const int threadCount = 10;
            const int incrementsPerThread = 20;
            const int expectedTotal = threadCount * incrementsPerThread;

            // Act: 10 concurrent tasks incrementing counter via optimistic CAS retry-loops against real Redis
            Task[] tasks = [.. Enumerable.Range(0, threadCount)
                .Select(_ => Task.Run(async () => {
                    for (int i = 0; i < incrementsPerThread; i++) {
                        while (!ct.IsCancellationRequested) {
                            CounterValue current = await this._storage.GetAsync(key, ct);
                            CounterValue next = current + 1;

                            if (await this._storage.TryCompareExchangeAsync(key, current, next, CounterExpiry.FromMinutes(2), ct)) {
                                break; // Successfully updated in Redis
                            }
                        }
                    }
                }, ct))];

            await Task.WhenAll(tasks);

            // Assert: Zero lost updates despite massive optimistic retry contention across real network socket
            CounterValue finalVal = await this._storage.GetAsync(key, ct);
            Assert.Equal(expectedTotal, finalVal.Value);
        }
    }
}