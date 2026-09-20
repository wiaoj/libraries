using StackExchange.Redis;
using Testcontainers.Redis;

namespace Wiaoj.Idempotency.Redis.Tests.Integration.Fixtures;

/// <summary>
/// Runs an isolated Redis for the suite.
/// </summary>
/// <remarks>
/// Set <c>WIAOJ_TEST_REDIS</c> to a connection string to use an existing Redis instead of starting a container, for
/// machines where Testcontainers can't reach the container engine.
/// </remarks>
public sealed class RedisTestFixture : IAsyncLifetime {
    private static readonly string? ExistingRedis = Environment.GetEnvironmentVariable("WIAOJ_TEST_REDIS");

    private readonly RedisContainer? _container = string.IsNullOrWhiteSpace(ExistingRedis)
        ? new RedisBuilder("redis:7-alpine").Build()
        : null;

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public string ConnectionString => this._container?.GetConnectionString() ?? ExistingRedis!;

    public async ValueTask InitializeAsync() {
        if(this._container is not null) {
            await this._container.StartAsync().ConfigureAwait(false);
        }

        this.Connection = await ConnectionMultiplexer.ConnectAsync(this.ConnectionString).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        if(this.Connection is not null) {
            await this.Connection.DisposeAsync().ConfigureAwait(false);
        }

        if(this._container is not null) {
            await this._container.DisposeAsync().ConfigureAwait(false);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class RedisTestCollection : ICollectionFixture<RedisTestFixture> {
    public const string Name = "RedisIdempotencyCollection";
}
