namespace Wiaoj.BloomFilter.Redis.Options;

/// <summary>
/// Configuration options for the Redis-backed Bloom Filter snapshot storage provider.
/// </summary>
public sealed class RedisBloomFilterStorageOptions {
    /// <summary>
    /// Gets or sets the Redis key prefix used for storing filter snapshots.
    /// Default is <c>"bloom:snapshot:"</c>.
    /// </summary>
    public string KeyPrefix { get; set; } = "bloom:snapshot:";

    /// <summary>
    /// Gets or sets the optional time-to-live (TTL) expiration for snapshot keys in Redis.
    /// Default is <see langword="null"/> (keys do not expire).
    /// </summary>
    public TimeSpan? Ttl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stream snapshots should be compressed using GZip before saving to Redis.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether non-fatal Redis errors should be swallowed and logged instead of thrown.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool IgnoreErrors { get; set; }

    /// <summary>
    /// Gets or sets the target Redis logical database index (-1 or <see langword="null"/> for default).
    /// </summary>
    public int? Database { get; set; }
}
