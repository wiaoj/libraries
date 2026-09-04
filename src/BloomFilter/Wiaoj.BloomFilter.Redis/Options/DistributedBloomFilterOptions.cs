namespace Wiaoj.BloomFilter.Redis.Options;

/// <summary>
/// Configuration options for distributed remote Redis Bloom Filters.
/// </summary>
public sealed class DistributedBloomFilterOptions {
    /// <summary>
    /// Gets or sets the Redis key prefix for distributed filter bit arrays.
    /// Default is <c>"bloom:live:"</c>.
    /// </summary>
    public string KeyPrefix { get; set; } = "bloom:live:";

    /// <summary>
    /// Gets or sets the target Redis logical database index (-1 or <see langword="null"/> for default).
    /// </summary>
    public int? Database { get; set; }
}
