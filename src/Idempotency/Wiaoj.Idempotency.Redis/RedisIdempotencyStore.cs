using StackExchange.Redis;

namespace Wiaoj.Idempotency.Redis;

/// <summary>
/// Redis-backed <see cref="IIdempotencyStore"/>. A claim is a key that exists, so it outlives the process that made it
/// and is shared by every replica.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TryMarkProcessedAsync"/> is one round trip and atomic in Redis itself: <c>SET key value NX PX ttl</c>
/// stores the key only if it does not exist, so exactly one caller wins for a key — the operation idempotency is built
/// on. Nothing is buffered: a buffered claim is not a claim.
/// </para>
/// <para>
/// While the multiplexer has no connection, every operation throws <see cref="RedisConnectionException"/> at once rather
/// than queueing the command until the backlog timeout expires. A claim that has not been recorded must be reported to
/// the caller, not waited on.
/// </para>
/// </remarks>
public sealed class RedisIdempotencyStore : IIdempotencyStore {
    // Claims are pure membership: the value is never read, so it stays one byte.
    private static readonly RedisValue ClaimMarker = "1";

    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private readonly int _database;

    /// <summary>Creates a store over a multiplexer with the default options.</summary>
    /// <param name="redis">The Redis connection.</param>
    public RedisIdempotencyStore(IConnectionMultiplexer redis) : this(redis, new RedisIdempotencyOptions()) {
    }

    /// <summary>Creates a store over a multiplexer.</summary>
    /// <param name="redis">The Redis connection.</param>
    /// <param name="options">The key prefix and database to use.</param>
    public RedisIdempotencyStore(IConnectionMultiplexer redis, RedisIdempotencyOptions options) {
        Preca.ThrowIfNull(redis);
        Preca.ThrowIfNull(options);

        this._redis = redis;
        this._keyPrefix = options.KeyPrefix;
        this._database = options.Database ?? -1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ContainsAsync(IdempotencyKey key, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        return await Database.KeyExistsAsync(RedisKeyFor(key)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask MarkProcessedAsync(IdempotencyKey key, TimeSpan window, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preca.ThrowIfNegativeOrZero(window);

        await Database.StringSetAsync(RedisKeyFor(key), ClaimMarker, window).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryMarkProcessedAsync(IdempotencyKey key, TimeSpan window, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preca.ThrowIfNegativeOrZero(window);

        // SET NX PX: stores the key only when absent, so the first caller wins and every other one is told it lost.
        return await Database.StringSetAsync(RedisKeyFor(key), ClaimMarker, window, When.NotExists).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(IdempotencyKey key, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        await Database.KeyDeleteAsync(RedisKeyFor(key)).ConfigureAwait(false);
    }

    /// <summary>The database to send a command to — or, while disconnected, a connection exception at once.</summary>
    private IDatabase Database {
        get {
            if(!this._redis.IsConnected) {
                throw new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    "Redis is not connected; the idempotency operation was not attempted rather than waiting for a connection.");
            }

            return this._redis.GetDatabase(this._database);
        }
    }

    /// <summary>Prefixes the key, so a claim can't collide with other data in a shared instance.</summary>
    internal RedisKey RedisKeyFor(IdempotencyKey key) {
        Preca.ThrowIfNullOrWhiteSpace(key.Value);

        return this._keyPrefix.Length == 0 ? key.Value : string.Concat(this._keyPrefix, key.Value);
    }
}
