using System.Text;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Root configuration object for Wiaoj Bloom Filter.
/// Acts as a pure DTO for binding from IConfiguration.
/// </summary>
public class BloomFilterOptions {
    /// <summary>
    /// The configuration section name in the application settings.
    /// </summary>
    public const string SectionName = "BloomFilter";

    /// <summary>
    /// Gets or sets the configuration for the storage mechanism (e.g., FileSystem).
    /// </summary>
    public StorageOptions Storage { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration for runtime performance tuning.
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration for lifecycle management (AutoSave, WarmUp, etc.).
    /// </summary>
    public LifecycleOptions Lifecycle { get; set; } = new();

    /// <summary>
    /// Gets or sets the dictionary of filter definitions defined in configuration.
    /// Key: Filter Name.
    /// </summary>
    public Dictionary<string, FilterDefinition> Filters { get; set; } = new();
}

/// <summary>
/// Options for configuring the persistence storage provider.
/// </summary>
public class StorageOptions {
    /// <summary>
    /// Gets or sets the storage provider type. Values: "FileSystem", "Redis" (Planned).
    /// Default: "FileSystem".
    /// </summary>
    public string Provider { get; set; } = "FileSystem";

    /// <summary>
    /// Gets or sets the base directory path for the FileSystem provider.
    /// </summary>
    public string Path { get; set; } = "BloomData";

    /// <summary>
    /// Gets or sets a value indicating whether the stored files should be compressed.
    /// Warning: Compressing high-entropy Bloom Filters may not yield significant gains but saves space for sparse ones.
    /// Default: false.
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Gets or sets the buffer size for file streams in bytes.
    /// Default: 81920 (80 KB).
    /// </summary>
    public int BufferSizeBytes { get; set; } = 81920;

    /// <summary>
    /// Gets or sets a value indicating whether to ignore storage read/write errors and operate in-memory only.
    /// Default: true.
    /// </summary>
    public bool IgnoreErrors { get; set; } = true;
}

/// <summary>
/// Options for tuning the runtime performance of the Bloom Filter.
/// </summary>
public class PerformanceOptions {
    /// <summary>
    /// Gets or sets a value indicating whether to enable SIMD (Single Instruction, Multiple Data) optimizations.
    /// Default: true.
    /// </summary>
    public bool EnableSimd { get; set; } = true;

    /// <summary>
    /// Gets or sets the global seed for the hash functions. Changing this invalidates all existing storage files.
    /// Default: 0.
    /// </summary>
    public long GlobalHashSeed { get; set; } = 0;
}

/// <summary>
/// Options for managing the lifecycle of Bloom Filters.
/// </summary>
public class LifecycleOptions {
    /// <summary>
    /// Gets or sets the interval for the auto-save background job.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to perform checksum validation when loading filters from disk.
    /// Default: true.
    /// </summary>
    public bool EnableIntegrityCheck { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to load all filters into RAM during application startup.
    /// Default: true.
    /// </summary>
    public bool EnableWarmUp { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically trigger registered seeders if data is missing or corrupted.
    /// Default: true.
    /// </summary>
    public bool AutoReseed { get; set; } = true;

    /// <summary>
    /// Gets or sets the threshold in bytes to automatically split a filter into shards.
    /// Default: 100 MB.
    /// </summary>
    public long ShardingThresholdBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically reset the filter if a fingerprint mismatch occurs during load.
    /// </summary>
    public bool AutoResetOnMismatch { get; set; } = true;
}

/// <summary>
/// Defines the supported implementation types of a Bloom Filter.
/// </summary>
public enum BloomFilterType { 
    /// <summary> Standard fixed-size in-memory filter. </summary>
    InMemory, 
    /// <summary> Dynamically expanding filter using multiple stages. </summary>
    Scalable, 
    /// <summary> Time-based windowed filter that retires old shards. </summary>
    Rotating 
}

/// <summary>
/// Represents the configuration parameters for a specific Bloom Filter instance.
/// </summary>
public class FilterDefinition {
    /// <summary> Gets or sets the expected number of items (n). </summary>
    public long ExpectedItems { get; set; }
    
    /// <summary> Gets or sets the desired false positive probability (p). </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the architectural type of the filter.
    /// </summary>
    public BloomFilterType Type { get; set; } = BloomFilterType.InMemory;

    /// <summary>
    /// Gets or sets the growth rate for Scalable filters.
    /// </summary>
    public double GrowthRate { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the time window size for Rotating filters.
    /// </summary>
    public TimeSpan WindowSize { get; set; }
    
    /// <summary>
    /// Gets or sets the number of active shards for windowing.
    /// </summary>
    public int ShardCount { get; set; }
}