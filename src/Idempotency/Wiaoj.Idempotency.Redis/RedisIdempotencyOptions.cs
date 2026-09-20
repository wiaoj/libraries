namespace Wiaoj.Idempotency.Redis;

/// <summary>
/// Options for the Redis-backed idempotency store.
/// </summary>
public sealed class RedisIdempotencyOptions {
    /// <summary>The default key prefix (<c>wiaoj:idemp:</c>).</summary>
    public const string DefaultKeyPrefix = "wiaoj:idemp:";

    private string _keyPrefix = DefaultKeyPrefix;
    private int? _database;

    /// <summary>
    /// Gets or sets the prefix every key is stored under, so a claim can't collide with a counter or a cache entry in a
    /// shared instance. Default is <see cref="DefaultKeyPrefix"/>; an empty prefix is allowed.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string KeyPrefix {
        get => this._keyPrefix;
        set {
            Preca.ThrowIfNull(value);
            this._keyPrefix = value;
        }
    }

    /// <summary>
    /// Gets or sets the Redis database index, or <see langword="null"/> for the connection's default.
    /// </summary>
    public int? Database {
        get => this._database;
        set {
            if(value.HasValue) {
                Preca.ThrowIfNegative(value.Value);
            }
            this._database = value;
        }
    }
}
