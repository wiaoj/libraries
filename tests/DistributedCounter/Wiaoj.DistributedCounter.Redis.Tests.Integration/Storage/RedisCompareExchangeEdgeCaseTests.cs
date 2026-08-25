using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Storage;

[Collection(RedisTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "CompareExchangeEdgeCases")]
public sealed class RedisCompareExchangeEdgeCaseTests {
    private readonly RedisCounterStorage _storage;

    public RedisCompareExchangeEdgeCaseTests(RedisTestFixture fixture) {
        this._storage = new RedisCounterStorage(fixture.Connection);
    }

    [Fact]
    public async Task TryCompareExchange_WithInfiniteExpiry_DoesNotCrashLuaScriptAndRemovesOldTtl() {
        // Arrange: Key with pre-existing 60s TTL
        CounterKey key = new($"redis:cas:edge:infinite:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;

        await this._storage.SetAsync(key, new CounterValue(10), CounterExpiry.FromSeconds(60), ct);
        Assert.NotNull(await this._storage.GetTtlAsync(key, ct));

        // Act: CAS with Infinite expiry (ttlMs = 0)
        bool success = await this._storage.TryCompareExchangeAsync(
            key,
            expectedValue: 10,
            newValue: 20,
            CounterExpiry.Infinite,
            ct);

        // Assert: Succeeded without Lua crash, value updated, TTL cleared to Infinite (null)
        Assert.True(success);
        Assert.Equal(20, (await this._storage.GetAsync(key, ct)).Value);
        Assert.Null(await this._storage.GetTtlAsync(key, ct));
    }

    [Fact]
    public async Task TryCompareExchange_WhenFailed_NeverMutatesKeyOrItsExistingTtl() {
        // Arrange
        CounterKey key = new($"redis:cas:edge:failed-nomutate:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;
        CounterExpiry originalExpiry = CounterExpiry.FromSeconds(120);

        await this._storage.SetAsync(key, new CounterValue(50), originalExpiry, ct);

        // Act: Attempt CAS with mismatch expected value and different 5s TTL
        bool success = await this._storage.TryCompareExchangeAsync(
            key,
            expectedValue: 999, // Wrong
            newValue: 100,
            CounterExpiry.FromSeconds(5),
            ct);

        // Assert
        Assert.False(success);
        Assert.Equal(50, (await this._storage.GetAsync(key, ct)).Value); // Value untouched

        TimeSpan? liveTtl = await this._storage.GetTtlAsync(key, ct);
        Assert.NotNull(liveTtl);
        Assert.True(liveTtl.Value.TotalSeconds > 100, "Original ~120s TTL must not be overwritten on failed CAS");
    }

    [Fact]
    public async Task TryCompareExchange_OnFreshKey_WithExpectedZero_InitializesKeyWithTtl() {
        // Arrange
        CounterKey key = new($"redis:cas:edge:fresh-zero:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act: Expect 0 on completely fresh key with 45s TTL
        bool success = await this._storage.TryCompareExchangeAsync(
            key,
            expectedValue: 0,
            newValue: 100,
            CounterExpiry.FromSeconds(45),
            ct);

        // Assert
        Assert.True(success);
        Assert.Equal(100, (await this._storage.GetAsync(key, ct)).Value);

        TimeSpan? liveTtl = await this._storage.GetTtlAsync(key, ct);
        Assert.NotNull(liveTtl);
        Assert.True(liveTtl.Value.TotalSeconds is > 40 and <= 45);
    }
}