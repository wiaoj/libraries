using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Storage;

[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "Storage")]
public sealed class RedisCounterStorageTests {

    [Collection(RedisTestCollection.Name)]
    public sealed class TheAtomicIncrementOperations {
        private readonly RedisCounterStorage _storage;

        public TheAtomicIncrementOperations(RedisTestFixture fixture) {
            this._storage = new RedisCounterStorage(fixture.Connection);
        }

        [Fact]
        public async Task AtomicIncrement_WithoutExpiry_IncrementsInRedisDirectly() {
            // Arrange
            CounterKey key = new($"redis:incr:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterValue v1 = await this._storage.AtomicIncrementAsync(key, 10, CounterExpiry.Infinite, ct);
            CounterValue v2 = await this._storage.AtomicIncrementAsync(key, 5, CounterExpiry.Infinite, ct);

            // Assert
            Assert.Equal(10, v1.Value);
            Assert.Equal(15, v2.Value);

            CounterValue directRead = await this._storage.GetAsync(key, ct);
            Assert.Equal(15, directRead.Value);
        }

        [Fact]
        public async Task AtomicIncrement_WithExpiry_SetsRedisPttlCorrectly() {
            // Arrange
            CounterKey key = new($"redis:incr:ttl:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;
            CounterExpiry expiry = CounterExpiry.FromSeconds(30);

            // Act
            CounterValue value = await this._storage.AtomicIncrementAsync(key, 1, expiry, ct);

            // Assert
            Assert.Equal(1, value.Value);

            TimeSpan? ttl = await this._storage.GetTtlAsync(key, ct);
            Assert.NotNull(ttl);
            Assert.True(ttl.Value.TotalSeconds is > 25 and <= 30);
        }
    }

    [Collection(RedisTestCollection.Name)]
    public sealed class TheLuaLimitOperations {
        private readonly RedisCounterStorage _storage;

        public TheLuaLimitOperations(RedisTestFixture fixture) {
            this._storage = new RedisCounterStorage(fixture.Connection);
        }

        [Fact]
        public async Task TryIncrement_LuaScript_EnforcesStrictLimitAndReturnsLivePttl() {
            // Arrange
            CounterKey key = new($"redis:limit:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;
            CounterExpiry expiry = CounterExpiry.FromSeconds(60);

            // Act 1: Initial increment within limit (4 out of 10)
            CounterLimitResult r1 = await this._storage.TryIncrementAsync(key, amount: 4, limit: 10, expiry, ct);

            // Assert 1
            Assert.True(r1.IsAllowed);
            Assert.Equal(4, r1.CurrentValue);
            Assert.Equal(6, r1.Remaining);
            Assert.NotNull(r1.Ttl);
            Assert.True(r1.Ttl.Value.TotalSeconds <= 60);

            // Act 2: Attempt increment that exceeds limit (4 + 7 = 11 > 10, rejected!)
            CounterLimitResult r2 = await this._storage.TryIncrementAsync(key, amount: 7, limit: 10, expiry, ct);

            // Assert 2
            Assert.False(r2.IsAllowed);
            Assert.Equal(4, r2.CurrentValue);
            Assert.Equal(0, r2.Remaining);
            Assert.NotNull(r2.Ttl);
        }

        [Fact]
        public async Task TryDecrement_LuaScript_EnforcesMinLimitCorrectly() {
            // Arrange
            CounterKey key = new($"redis:minlimit:{Guid.NewGuid():N}");
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Seed Redis with 10
            await this._storage.SetAsync(key, new CounterValue(10), CounterExpiry.Infinite, ct);

            // Act 1: Decrement 4 with min limit 2 (10 - 4 = 6 >= 2 -> Allowed)
            CounterLimitResult r1 = await this._storage.TryDecrementAsync(key, amount: 4, minLimit: 2, CounterExpiry.Infinite, ct);

            // Assert 1
            Assert.True(r1.IsAllowed);
            Assert.Equal(6, r1.CurrentValue);
            Assert.Equal(4, r1.Remaining);

            // Act 2: Decrement 5 with min limit 2 (6 - 5 = 1 < 2 -> Rejected!)
            CounterLimitResult r2 = await this._storage.TryDecrementAsync(key, amount: 5, minLimit: 2, CounterExpiry.Infinite, ct);

            // Assert 2
            Assert.False(r2.IsAllowed);
            Assert.Equal(6, r2.CurrentValue);
            Assert.Equal(0, r2.Remaining);
        }
    }

    [Collection(RedisTestCollection.Name)]
    public sealed class TheBatchAndMultiOperations {
        private readonly RedisCounterStorage _storage;

        public TheBatchAndMultiOperations(RedisTestFixture fixture) {
            this._storage = new RedisCounterStorage(fixture.Connection);
        }

        [Fact]
        public async Task BatchIncrementAsync_PipelinedExecution_IncrementsMultipleKeysInSingleRoundtrip() {
            // Arrange
            CounterKey k1 = new($"redis:batch:{Guid.NewGuid():N}:1");
            CounterKey k2 = new($"redis:batch:{Guid.NewGuid():N}:2");
            CancellationToken ct = TestContext.Current.CancellationToken;

            CounterUpdate[] updates = [
                new CounterUpdate(k1, 10, CounterExpiry.Infinite),
                new CounterUpdate(k2, 20, CounterExpiry.FromMinutes(5)),
                new CounterUpdate(k1, 5, CounterExpiry.Infinite)
            ];

            long[] results = new long[3];

            // Act
            await this._storage.BatchIncrementAsync(updates.AsMemory(), results.AsMemory(), ct);

            // Assert
            Assert.Equal(10, results[0]);
            Assert.Equal(20, results[1]);
            Assert.Equal(15, results[2]);

            Assert.Equal(15, (await this._storage.GetAsync(k1, ct)).Value);
            Assert.Equal(20, (await this._storage.GetAsync(k2, ct)).Value);
        }

        [Fact]
        public async Task GetManyAsync_MemoryOverload_FetchesValuesUsingRedisMGet() {
            // Arrange
            CounterKey k1 = new($"redis:mget:{Guid.NewGuid():N}:1");
            CounterKey k2 = new($"redis:mget:{Guid.NewGuid():N}:2");
            CancellationToken ct = TestContext.Current.CancellationToken;

            await this._storage.SetAsync(k1, new CounterValue(42), CounterExpiry.Infinite, ct);
            await this._storage.SetAsync(k2, new CounterValue(99), CounterExpiry.Infinite, ct);

            CounterKey[] keys = [k1, k2, new CounterKey("redis:missing")];
            CounterValue[] destinations = new CounterValue[3];

            // Act
            await this._storage.GetManyAsync(keys.AsMemory(), destinations.AsMemory(), ct);

            // Assert
            Assert.Equal(42, destinations[0].Value);
            Assert.Equal(99, destinations[1].Value);
            Assert.Equal(0, destinations[2].Value);
        }
    }
}