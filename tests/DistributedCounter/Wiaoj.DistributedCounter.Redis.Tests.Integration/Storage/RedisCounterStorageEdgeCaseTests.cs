using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Storage;

[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "Storage")]
[Collection(RedisTestCollection.Name)]
public sealed class RedisCounterStorageEdgeCaseTests {
    private readonly RedisCounterStorage _storage;

    public RedisCounterStorageEdgeCaseTests(RedisTestFixture fixture) {
        this._storage = new RedisCounterStorage(fixture.Connection);
    }

    // -----------------------------------------------------------------
    // RISK 1: CounterExpiry.Infinite + TryIncrementAsync on a BRAND NEW key.
    //
    // If GetTtlMilliseconds() returns a sentinel like -1 for "Infinite",
    // and the Lua script does:
    //     if ARGV[3] and ARGV[3] ~= '0' then redis.call('PEXPIRE', KEYS[1], ARGV[3]) end
    // then "-1" is a truthy, non-"0" string in Lua, so PEXPIRE key -1 fires,
    // which deletes the key IMMEDIATELY (PEXPIRE with a non-positive value
    // deletes the key in Redis). The counter would silently vanish right
    // after being created.
    // -----------------------------------------------------------------
    [Fact]
    public async Task TryIncrement_WithInfiniteExpiry_OnFreshKey_MustNotExpireOrDeleteKey() {
        // Arrange
        CounterKey key = new($"redis:edge:infinite-fresh:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act: first-ever write to this key, with Infinite expiry
        CounterLimitResult result = await this._storage.TryIncrementAsync(
            key, amount: 1, limit: 100, CounterExpiry.Infinite, ct);

        // Assert 1: the operation itself must report success correctly
        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.CurrentValue);

        // Assert 2: the key must still exist right after creation.
        // If PEXPIRE key -1 (or 0) fired, this GetAsync would come back as 0/missing.
        CounterValue readBack = await this._storage.GetAsync(key, ct);
        Assert.Equal(1, readBack.Value);

        // Assert 3: TTL must be persistent (null / -1), never a tiny/negative TTL.
        TimeSpan? ttl = await this._storage.GetTtlAsync(key, ct);
        Assert.Null(ttl); // StackExchange.Redis returns null for a key with no expiry
    }

    [Fact]
    public async Task TryDecrement_WithInfiniteExpiry_OnFreshKey_MustNotExpireOrDeleteKey() {
        // Arrange
        CounterKey key = new($"redis:edge:infinite-fresh-dec:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Seed with a value so a subsequent decrement has something to work with,
        // but seed WITHOUT expiry-setting code path (SetAsync), then immediately
        // exercise TryDecrementAsync with Infinite to hit the "current == 0" branch
        // on a key that was never touched by the Lua script before.
        await this._storage.SetAsync(key, new CounterValue(10), CounterExpiry.Infinite, ct);

        // Act
        CounterLimitResult result = await this._storage.TryDecrementAsync(
            key, amount: 3, minLimit: 0, CounterExpiry.Infinite, ct);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(7, result.CurrentValue);

        TimeSpan? ttl = await this._storage.GetTtlAsync(key, ct);
        Assert.Null(ttl);

        CounterValue readBack = await this._storage.GetAsync(key, ct);
        Assert.Equal(7, readBack.Value);
    }

    // -----------------------------------------------------------------
    // RISK 2: the `current == 0` heuristic used to detect "this is a brand
    // new key" is wrong when a counter has been legitimately reset to zero
    // but NOT deleted, and already carries its own TTL. A later
    // TryIncrementAsync call would incorrectly treat it as "new" and
    // OVERWRITE (reset) the existing TTL.
    // -----------------------------------------------------------------
    [Fact]
    public async Task TryIncrement_OnExistingKeyThatIsCurrentlyZero_MustNotResetPreExistingTtl() {
        // Arrange
        CounterKey key = new($"redis:edge:zero-value-existing-ttl:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Seed the key at value 0 with a LONG ttl (simulates "counter reset to 0
        // but window not yet expired" — a very common real-world state).
        CounterExpiry longExpiry = CounterExpiry.FromSeconds(120);
        await this._storage.SetAsync(key, new CounterValue(0), longExpiry, ct);

        TimeSpan? ttlBefore = await this._storage.GetTtlAsync(key, ct);
        Assert.NotNull(ttlBefore);
        Assert.True(ttlBefore.Value.TotalSeconds is > 110 and <= 120);

        // Act: increment with a SHORT expiry passed in. If the Lua script's
        // `current == 0` heuristic fires, it will treat this as "new key"
        // and overwrite the 120s TTL with this 5s TTL.
        CounterExpiry shortExpiry = CounterExpiry.FromSeconds(5);
        CounterLimitResult result = await this._storage.TryIncrementAsync(
            key, amount: 1, limit: 100, shortExpiry, ct);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.CurrentValue);

        // Assert: the pre-existing long TTL must survive untouched.
        TimeSpan? ttlAfter = await this._storage.GetTtlAsync(key, ct);
        Assert.NotNull(ttlAfter);
        Assert.True(
            ttlAfter.Value.TotalSeconds > 10,
            $"Expected the original ~120s TTL to survive, but it was reset to ~{ttlAfter.Value.TotalSeconds}s " +
            "— this indicates the Lua script's `current == 0` check is being used as a (wrong) proxy for " +
            "\"key does not exist\"."
        );
    }

    // -----------------------------------------------------------------
    // RISK 3 (regression guard for the bug you already found): rejected
    // operations must NEVER mutate the stored value, regardless of
    // direction (increment above limit / decrement below minLimit) and
    // regardless of whether the key had a finite or infinite expiry.
    // -----------------------------------------------------------------
    [Fact]
    public async Task TryIncrement_Rejected_NeverMutatesStoredValue_EvenWithInfiniteExpiry() {
        // Arrange
        CounterKey key = new($"redis:edge:rejected-no-mutate:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this._storage.SetAsync(key, new CounterValue(9), CounterExpiry.Infinite, ct);

        // Act: 9 + 5 = 14 > limit(10) -> must be rejected
        CounterLimitResult result = await this._storage.TryIncrementAsync(
            key, amount: 5, limit: 10, CounterExpiry.Infinite, ct);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(9, result.CurrentValue);

        CounterValue readBack = await this._storage.GetAsync(key, ct);
        Assert.Equal(9, readBack.Value); // Redis must be untouched

        TimeSpan? ttl = await this._storage.GetTtlAsync(key, ct);
        Assert.Null(ttl); // still infinite, was never touched
    }
}