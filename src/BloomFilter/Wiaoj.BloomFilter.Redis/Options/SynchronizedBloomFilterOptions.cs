namespace Wiaoj.BloomFilter.Redis.Options;

/// <summary>
/// Configuration options for hybrid synchronized Redis Bloom Filters.
/// </summary>
public sealed class SynchronizedBloomFilterOptions {
    /// <summary>
    /// Gets or sets the Redis Pub/Sub channel prefix used for delta replication.
    /// Default is <c>"bloom:sync:"</c>.
    /// </summary>
    public string SyncChannelPrefix { get; set; } = "bloom:sync:";

    /// <summary>
    /// Gets or sets the unique node identifier used to distinguish self-published messages from peer messages.
    /// If <see langword="null"/>, a new <see cref="Guid"/> is generated automatically.
    /// </summary>
    public Guid? NodeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether snapshot persistence to <see cref="IBloomFilterStorage"/> is enabled.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnableSnapshotPersistence { get; set; } = true;
}
