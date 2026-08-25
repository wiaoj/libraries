using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Storage;

[Collection(RedisTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "Concurrency")]
public sealed class RedisConcurrencyTests {
    private readonly RedisCounterStorage _storage;

    public RedisConcurrencyTests(RedisTestFixture fixture) {
        this._storage = new RedisCounterStorage(fixture.Connection);
    }

    [Fact]
    public async Task ConcurrentTryIncrements_UnderHighContention_NeverExceedStrictLimitInRedis() {
        // Arrange
        CounterKey key = new($"redis:concurrent:{Guid.NewGuid():N}");
        CancellationToken ct = TestContext.Current.CancellationToken;
        const int limit = 50;
        const int totalAttempts = 200;

        int allowedCount = 0;

        // Act: 200 concurrent tasks competing for 50 limit spots on real Redis
        Task[] tasks = [.. Enumerable.Range(0, totalAttempts)
            .Select(_ => Task.Run(async () => {
                CounterLimitResult res = await this._storage.TryIncrementAsync(key, amount: 1, limit: limit, CounterExpiry.FromMinutes(1), ct);
                if (res.IsAllowed) {
                    Interlocked.Increment(ref allowedCount);
                }
            }, ct))];

        await Task.WhenAll(tasks);

        // Assert: Real Redis Lua script guaranteed exact atomicity (Zero race condition breaches)
        CounterValue finalVal = await this._storage.GetAsync(key, ct);
        Assert.Equal(limit, allowedCount);
        Assert.Equal(limit, finalVal.Value);
    }
}