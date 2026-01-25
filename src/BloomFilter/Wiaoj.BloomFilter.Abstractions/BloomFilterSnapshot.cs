namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents a snapshot of a Bloom Filter's state at a specific point in time.
/// Used primarily for internal serialization and transport between layers.
/// </summary>
public sealed record BloomFilterSnapshot {
    /// <summary>
    /// Gets the configuration associated with this snapshot.
    /// </summary>
    public required BloomFilterConfiguration Config { get; init; }

    /// <summary>
    /// Gets the raw binary data of the bit array.
    /// </summary>
    /// <remarks>
    /// This byte array contains the actual 0s and 1s of the filter. 
    /// For large filters, this can be significant in size.
    /// </remarks>
    public required byte[] Bits { get; init; }

    /// <summary>
    /// Gets the checksum (e.g., XXHash64) of the <see cref="Bits"/> for data integrity verification.
    /// </summary>
    public ulong Checksum { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this snapshot was created or updated.
    /// </summary>
    public required DateTimeOffset LastUpdated { get; init; }
}