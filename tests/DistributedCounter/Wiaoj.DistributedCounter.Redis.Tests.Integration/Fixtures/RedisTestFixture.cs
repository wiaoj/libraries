using StackExchange.Redis;
using Testcontainers.Redis;

namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

/// <summary>
/// Manages the lifecycle of an isolated Redis Testcontainer for integration tests.
/// Compatible with both Docker and Podman environments.
/// </summary>
public sealed class RedisTestFixture : IAsyncLifetime {
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;
    public string ConnectionString => this._container.GetConnectionString();

    public async ValueTask InitializeAsync() {
        // Start the temporary Redis container
        await this._container.StartAsync().ConfigureAwait(false);

        // Connect StackExchange.Redis to the container
        this.Connection = await ConnectionMultiplexer.ConnectAsync(this.ConnectionString).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        if(this.Connection is not null) {
            await this.Connection.DisposeAsync().ConfigureAwait(false);
        }

        await this._container.DisposeAsync().ConfigureAwait(false);
    }
}