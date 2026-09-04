namespace Wiaoj.BloomFilter;

/// <summary>
/// Root configuration options for Bloom Filter services.
/// </summary>
public class BloomFilterOptions {
    /// <summary>
    /// The default configuration section name in application settings.
    /// </summary>
    public const string SectionName = "BloomFilter";

    /// <summary>
    /// Default global hash seed used when individual filters do not specify one.
    /// If null, the factory's internal default seed is used.
    /// </summary>
    public long? DefaultHashSeed { get; set; }

    /// <summary>
    /// Lifecycle and background worker options.
    /// </summary>
    public LifecycleOptions Lifecycle { get; set; } = new();

    /// <summary>
    /// Dictionary of configured filter definitions keyed by filter name.
    /// </summary>
    public Dictionary<string, FilterDefinition> Filters { get; set; } = [];
}

/// <summary>
/// Lifecycle and maintenance options for registered filters.
/// </summary>
public class LifecycleOptions {
    /// <summary>
    /// Interval between automatic background persistence cycles. Default: 5 minutes.
    /// </summary>
    public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Indicates whether checksum verification is performed during filter reloads.
    /// </summary>
    public bool EnableIntegrityCheck { get; set; } = true;

    /// <summary>
    /// Indicates whether all registered filters are preloaded into memory on startup.
    /// </summary>
    public bool EnableWarmUp { get; set; } = true;

    /// <summary>
    /// Indicates whether auto-seeders are triggered when data is corrupted or missing.
    /// </summary>
    public bool AutoReseed { get; set; } = true;

    /// <summary>
    /// Threshold in bytes to automatically split large filters into multiple shards. Default: 100 MB.
    /// </summary>
    public long ShardingThresholdBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Indicates whether to reinitialize an empty filter if configuration fingerprint mismatch occurs.
    /// </summary>
    public bool AutoResetOnMismatch { get; set; } = true;
}

/// <summary>
/// Architectural variant of a Bloom Filter instance.
/// </summary>
public enum BloomFilterType {
    /// <summary> Standard fixed-capacity filter (single or auto-sharded). </summary>
    InMemory,
    /// <summary> Dynamically layered filter that scales as saturation increases. </summary>
    Scalable,
    /// <summary> Time-windowed sliding filter with rotating shards. </summary>
    Rotating
}

/// <summary>
/// Definition parameters for a specific named Bloom Filter.
/// </summary>
public class FilterDefinition {
    /// <summary> Expected item capacity (n). </summary>
    public long ExpectedItems { get; set; }

    /// <summary> Target false positive probability (p), strictly between 0 and 1. </summary>
    public double ErrorRate { get; set; }

    /// <summary> The architectural filter type. </summary>
    public BloomFilterType Type { get; set; } = BloomFilterType.InMemory;

    /// <summary> Capacity multiplier for newly spawned layers in Scalable filters. </summary>
    public double GrowthRate { get; set; } = 2.0;

    /// <summary> Saturation threshold (0.0 to 1.0) that triggers a new layer in Scalable filters. </summary>
    public double SaturationThreshold { get; set; } = 0.50;

    /// <summary> Total time window duration for Rotating filters. </summary>
    public TimeSpan WindowSize { get; set; }

    /// <summary> Total number of active sliding shards for Rotating filters. </summary>
    public int ShardCount { get; set; }
}