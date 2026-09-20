using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Wiaoj.Idempotency.Redis.Tests.Integration;

/// <summary>
/// With Redis unreachable, every operation fails at once instead of waiting out StackExchange.Redis' backlog timeout, so
/// a caller decides what an unavailable store means for it without stalling first (#52). Needs no Redis: the multiplexer
/// points at a closed port.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Redis")]
[Trait("Feature", "Idempotency")]
public sealed class RedisIdempotencyStoreUnreachableTests : IAsyncLifetime {
    // A backlog timeout well above the bound asserted below, so a queued command can't pass by timing out.
    private const string Unreachable = "localhost:1,abortConnect=false,connectTimeout=500,asyncTimeout=5000,syncTimeout=5000";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IConnectionMultiplexer _multiplexer = null!;

    public async ValueTask InitializeAsync() {
        this._multiplexer = await ConnectionMultiplexer.ConnectAsync(Unreachable);
    }

    public async ValueTask DisposeAsync() {
        await this._multiplexer.DisposeAsync();
    }

    public static TheoryData<string> Operations => ["TryMarkProcessed", "Contains", "MarkProcessed", "Remove"];

    private static ValueTask Invoke(RedisIdempotencyStore store, string operation) {
        IdempotencyKey key = new($"unreachable:{Guid.NewGuid():N}");
        TimeSpan window = TimeSpan.FromMinutes(5);

        return operation switch {
            "TryMarkProcessed" => Discard(store.TryMarkProcessedAsync(key, window, Ct)),
            "Contains" => Discard(store.ContainsAsync(key, Ct)),
            "MarkProcessed" => store.MarkProcessedAsync(key, window, Ct),
            "Remove" => store.RemoveAsync(key, Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        static async ValueTask Discard<T>(ValueTask<T> call) => await call;
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task EveryOperation_FailsImmediately_WithAConnectionException(string operation) {
        RedisIdempotencyStore store = new(this._multiplexer);
        Assert.False(this._multiplexer.IsConnected);

        Stopwatch elapsed = Stopwatch.StartNew();
        RedisConnectionException error = await Assert.ThrowsAsync<RedisConnectionException>(
            () => Invoke(store, operation).AsTask());
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < Bound, $"{operation} took {elapsed.Elapsed.TotalMilliseconds:F0} ms with Redis unreachable.");
        Assert.Equal(ConnectionFailureType.UnableToConnect, error.FailureType);
    }

    [Fact]
    public async Task ManyClaims_TakeFarLessThanOneTimeoutEach() {
        RedisIdempotencyStore store = new(this._multiplexer);

        Stopwatch elapsed = Stopwatch.StartNew();
        for(int i = 0; i < 20; i++) {
            await Assert.ThrowsAsync<RedisConnectionException>(() => Invoke(store, "TryMarkProcessed").AsTask());
        }
        elapsed.Stop();

        Assert.True(elapsed.Elapsed < Bound, $"20 claims took {elapsed.Elapsed.TotalMilliseconds:F0} ms with Redis unreachable.");
    }

    public sealed class TheRegistration {
        [Fact]
        public void ResolvesTheStoreFromAConnectionString() {
            ServiceCollection services = new();
            services.AddRedisIdempotencyStore(Unreachable, options => options.KeyPrefix = "custom:");

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsType<RedisIdempotencyStore>(provider.GetRequiredService<IIdempotencyStore>());
        }

        [Fact]
        public async Task UsesTheMultiplexerAlreadyRegistered() {
            ServiceCollection services = new();
            await using IConnectionMultiplexer shared = await ConnectionMultiplexer.ConnectAsync(Unreachable);
            services.AddSingleton(shared);
            services.AddRedisIdempotencyStore();

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsType<RedisIdempotencyStore>(provider.GetRequiredService<IIdempotencyStore>());
        }

        [Fact]
        public async Task ResolvesAKeyedMultiplexer() {
            ServiceCollection services = new();
            await using IConnectionMultiplexer claims = await ConnectionMultiplexer.ConnectAsync(Unreachable);
            services.AddKeyedSingleton("claims", claims);
            services.AddKeyedRedisIdempotencyStore("claims");

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsType<RedisIdempotencyStore>(provider.GetRequiredService<IIdempotencyStore>());
        }

        [Fact]
        public void RefusesMissingArguments() {
            ServiceCollection services = new();

            Assert.ThrowsAny<ArgumentException>(() => services.AddRedisIdempotencyStore((string)null!));
            Assert.ThrowsAny<ArgumentException>(() => services.AddRedisIdempotencyStore((ConfigurationOptions)null!));
            Assert.ThrowsAny<ArgumentException>(() => services.AddKeyedRedisIdempotencyStore(null!));
        }
    }
}
