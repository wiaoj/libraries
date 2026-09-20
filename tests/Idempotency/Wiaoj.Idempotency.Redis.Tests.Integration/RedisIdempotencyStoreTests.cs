using StackExchange.Redis;
using Wiaoj.Idempotency.Redis.Tests.Integration.Fixtures;

namespace Wiaoj.Idempotency.Redis.Tests.Integration;

/// <summary>
/// A claim is a key that exists: recorded atomically, shared by every connection, and gone when its window expires (#52).
/// </summary>
[Collection(RedisTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "Idempotency")]
public sealed class RedisIdempotencyStoreTests(RedisTestFixture fixture) {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private static IdempotencyKey NewKey() => new($"test:{Guid.NewGuid():N}");

    private RedisIdempotencyStore Store(string? prefix = null) {
        return new RedisIdempotencyStore(
            fixture.Connection,
            prefix is null ? new RedisIdempotencyOptions() : new RedisIdempotencyOptions { KeyPrefix = prefix });
    }

    [Fact]
    public async Task TheFirstClaimWins_AndTheSecondIsTold() {
        RedisIdempotencyStore store = Store();
        IdempotencyKey key = NewKey();

        Assert.True(await store.TryMarkProcessedAsync(key, Window, Ct));
        Assert.False(await store.TryMarkProcessedAsync(key, Window, Ct));
        Assert.True(await store.ContainsAsync(key, Ct));
    }

    [Fact]
    public async Task TwoReplicasRacingOneKey_ProduceExactlyOneWinner() {
        // Separate stores over the same Redis stand in for two replicas.
        RedisIdempotencyStore[] replicas = [Store(), Store(), Store(), Store()];
        IdempotencyKey key = NewKey();

        bool[] results = await Task.WhenAll(replicas.Select(async replica => {
            await Task.Yield();
            return await replica.TryMarkProcessedAsync(key, Window, Ct);
        }));

        Assert.Single(results, won => won);
    }

    [Fact]
    public async Task AClaimSurvivesANewConnection() {
        IdempotencyKey key = NewKey();
        Assert.True(await Store().TryMarkProcessedAsync(key, Window, Ct));

        // What an in-memory store cannot do: the claim outlives the process that made it.
        await using IConnectionMultiplexer reconnected = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        RedisIdempotencyStore afterRestart = new(reconnected);

        Assert.False(await afterRestart.TryMarkProcessedAsync(key, Window, Ct));
        Assert.True(await afterRestart.ContainsAsync(key, Ct));
    }

    [Fact]
    public async Task AClaimExpires_WhenItsWindowPasses() {
        RedisIdempotencyStore store = Store();
        IdempotencyKey key = NewKey();

        Assert.True(await store.TryMarkProcessedAsync(key, TimeSpan.FromMilliseconds(300), Ct));
        Assert.True(await store.ContainsAsync(key, Ct));

        await Task.Delay(TimeSpan.FromMilliseconds(700), Ct);

        Assert.False(await store.ContainsAsync(key, Ct));
        Assert.True(await store.TryMarkProcessedAsync(key, Window, Ct));
    }

    [Fact]
    public async Task RemovingAClaim_LetsTheWorkRunAgain() {
        RedisIdempotencyStore store = Store();
        IdempotencyKey key = NewKey();

        Assert.True(await store.TryMarkProcessedAsync(key, Window, Ct));
        await store.RemoveAsync(key, Ct);

        Assert.False(await store.ContainsAsync(key, Ct));
        Assert.True(await store.TryMarkProcessedAsync(key, Window, Ct));
    }

    [Fact]
    public async Task MarkProcessed_RecordsTheKeyEvenWhenItIsAlreadyClaimed() {
        RedisIdempotencyStore store = Store();
        IdempotencyKey key = NewKey();

        await store.MarkProcessedAsync(key, Window, Ct);
        await store.MarkProcessedAsync(key, Window, Ct);

        Assert.True(await store.ContainsAsync(key, Ct));
        Assert.False(await store.TryMarkProcessedAsync(key, Window, Ct));
    }

    [Fact]
    public async Task ClaimsAreNamespacedByPrefix() {
        IdempotencyKey key = NewKey();
        RedisIdempotencyStore orders = Store("orders:idemp:");
        RedisIdempotencyStore payments = Store("payments:idemp:");

        Assert.True(await orders.TryMarkProcessedAsync(key, Window, Ct));

        // The same key under another prefix is a different claim.
        Assert.True(await payments.TryMarkProcessedAsync(key, Window, Ct));
        Assert.True(await fixture.Connection.GetDatabase().KeyExistsAsync($"orders:idemp:{key.Value}"));
        Assert.True(await fixture.Connection.GetDatabase().KeyExistsAsync($"payments:idemp:{key.Value}"));
        Assert.False(await fixture.Connection.GetDatabase().KeyExistsAsync(key.Value));
    }

    [Fact]
    public async Task AnEmptyPrefix_StoresTheKeyAsItIs() {
        IdempotencyKey key = NewKey();

        Assert.True(await Store(string.Empty).TryMarkProcessedAsync(key, Window, Ct));

        Assert.True(await fixture.Connection.GetDatabase().KeyExistsAsync(key.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ANonPositiveWindow_IsRefused(int seconds) {
        RedisIdempotencyStore store = Store();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => store.TryMarkProcessedAsync(NewKey(), TimeSpan.FromSeconds(seconds), Ct).AsTask());
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => store.MarkProcessedAsync(NewKey(), TimeSpan.FromSeconds(seconds), Ct).AsTask());
    }

    [Fact]
    public async Task ACancelledToken_WinsOverTheCall() {
        RedisIdempotencyStore store = Store();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.TryMarkProcessedAsync(NewKey(), Window, cts.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.ContainsAsync(NewKey(), cts.Token).AsTask());
    }

    [Fact]
    public void TheStoreNeedsAConnectionAndOptions() {
        Assert.ThrowsAny<ArgumentException>(() => new RedisIdempotencyStore(null!));
        Assert.ThrowsAny<ArgumentException>(() => new RedisIdempotencyStore(fixture.Connection, null!));
        Assert.ThrowsAny<ArgumentException>(() => new RedisIdempotencyOptions { KeyPrefix = null! });
        Assert.ThrowsAny<ArgumentException>(() => new RedisIdempotencyOptions { Database = -2 });
    }
}
