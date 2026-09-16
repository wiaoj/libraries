using System.Diagnostics;
using StackExchange.Redis;
using Wiaoj.DistributedCounter.Redis.Internal;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Storage;

/// <summary>
/// With Redis unreachable, every storage call fails at once instead of waiting out StackExchange.Redis' backlog timeout,
/// so claims, circuits, pacing and frequency caps can apply their own fail-open or fail-closed rule without a 5 s stall
/// per call. Needs no Redis: the multiplexer points at a closed port.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "Storage")]
public sealed class RedisCounterStorageUnreachableTests : IAsyncLifetime {
    // A backlog timeout well above the bound asserted below, so a queued command can't pass by timing out.
    private const string Unreachable = "localhost:1,abortConnect=false,connectTimeout=500,asyncTimeout=5000,syncTimeout=5000";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(1);

    private IConnectionMultiplexer _multiplexer = null!;

    public async ValueTask InitializeAsync() {
        this._multiplexer = await ConnectionMultiplexer.ConnectAsync(Unreachable);
    }

    public async ValueTask DisposeAsync() {
        await this._multiplexer.DisposeAsync();
    }

    public static TheoryData<string> Operations => [
        nameof(ICounterStorage.AtomicIncrementAsync),
        nameof(ICounterStorage.AtomicIncrementAsync) + ":ttl",
        nameof(ICounterStorage.TryIncrementAsync),
        nameof(ICounterStorage.TryDecrementAsync),
        nameof(ICounterStorage.TryCompareExchangeAsync),
        nameof(ICounterStorage.GetAsync),
        nameof(ICounterStorage.GetTtlAsync),
        nameof(ICounterStorage.GetManyAsync),
        nameof(ICounterStorage.GetManyAsync) + ":memory",
        nameof(ICounterStorage.DeleteAsync),
        nameof(ICounterStorage.SetAsync),
        nameof(ICounterStorage.BatchIncrementAsync),
    ];

    private static ValueTask Invoke(ICounterStorage storage, string operation, CancellationToken ct) {
        CounterKey key = new($"unreachable:{Guid.NewGuid():N}");
        CounterExpiry ttl = CounterExpiry.FromSeconds(60);

        return operation switch {
            "AtomicIncrementAsync" => Discard(storage.AtomicIncrementAsync(key, 1, CounterExpiry.Infinite, ct)),
            "AtomicIncrementAsync:ttl" => Discard(storage.AtomicIncrementAsync(key, 1, ttl, ct)),
            "TryIncrementAsync" => Discard(storage.TryIncrementAsync(key, 1, 10, ttl, ct)),
            "TryDecrementAsync" => Discard(storage.TryDecrementAsync(key, 1, 0, ttl, ct)),
            "TryCompareExchangeAsync" => Discard(storage.TryCompareExchangeAsync(key, new CounterValue(0), new CounterValue(1), ttl, ct)),
            "GetAsync" => Discard(storage.GetAsync(key, ct)),
            "GetTtlAsync" => Discard(storage.GetTtlAsync(key, ct)),
            "GetManyAsync" => Discard(storage.GetManyAsync([key], ct)),
            "GetManyAsync:memory" => storage.GetManyAsync(new[] { key }, new CounterValue[1], ct),
            "DeleteAsync" => storage.DeleteAsync(key, ct),
            "SetAsync" => storage.SetAsync(key, new CounterValue(1), ttl, ct),
            "BatchIncrementAsync" => storage.BatchIncrementAsync(new[] { new CounterUpdate(key, 1, ttl) }, new long[1], ct),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        static async ValueTask Discard<T>(ValueTask<T> call) => await call;
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task EveryOperation_FailsImmediately_WithAConnectionException(string operation) {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisCounterStorage storage = new(this._multiplexer);
        Assert.False(this._multiplexer.IsConnected);

        Stopwatch elapsed = Stopwatch.StartNew();
        RedisConnectionException error = await Assert.ThrowsAsync<RedisConnectionException>(() => Invoke(storage, operation, ct).AsTask());
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < Bound, $"{operation} took {elapsed.Elapsed.TotalMilliseconds:F0} ms with Redis unreachable.");
        Assert.Equal(ConnectionFailureType.UnableToConnect, error.FailureType);
    }

    [Fact]
    public async Task ManyCalls_TakeFarLessThanOneTimeoutEach() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RedisCounterStorage storage = new(this._multiplexer);

        Stopwatch elapsed = Stopwatch.StartNew();
        for(int i = 0; i < 20; i++) {
            await Assert.ThrowsAsync<RedisConnectionException>(() => Invoke(storage, nameof(ICounterStorage.TryIncrementAsync), ct).AsTask());
        }
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < Bound, $"20 calls took {elapsed.Elapsed.TotalMilliseconds:F0} ms with Redis unreachable.");
    }

    [Fact]
    public async Task ACancelledToken_StillWins_OverTheConnectionCheck() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new RedisCounterStorage(this._multiplexer).GetAsync(new CounterKey("unreachable:cancelled"), cts.Token).AsTask());
    }
}
