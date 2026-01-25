using System.Text;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Root configuration object for Wiaoj Bloom Filter.
/// Acts as a pure DTO for binding from IConfiguration.
/// </summary>
public class BloomFilterOptions {
    /// <summary>
    /// Configuration for storage mechanism (FileSystem, Redis, etc.).
    /// </summary>
    public StorageOptions Storage { get; set; } = new();

    /// <summary>
    /// Configuration for runtime performance tuning.
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();

    /// <summary>
    /// Configuration for lifecycle management (AutoSave, WarmUp).
    /// </summary>
    public LifecycleOptions Lifecycle { get; set; } = new();

    /// <summary>
    /// Dictionary of filter definitions defined in configuration.
    /// Key: Filter Name.
    /// </summary>
    public Dictionary<string, FilterDefinition> Filters { get; set; } = new();
}

public class StorageOptions {
    /// <summary>
    /// The storage provider type. Values: "FileSystem", "Redis" (Future).
    /// Default: "FileSystem".
    /// </summary>
    public string Provider { get; set; } = "FileSystem";

    /// <summary>
    /// Base directory path for FileSystem provider.
    /// </summary>
    public string Path { get; set; } = "BloomData";

    /// <summary>
    /// Determines if the stored files should be compressed (e.g. GZip).
    /// Warning: Compressing high-entropy bloom filters might not yield significant gains but saves space for sparse ones.
    /// Default: false.
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Buffer size for file streams in bytes.
    /// Default: 81920 (80 KB).
    /// </summary>
    public int BufferSizeBytes { get; set; } = 81920;

    /// <summary>
    /// If true, ignores storage read/write errors and operates in-memory only.
    /// Default: true.
    /// </summary>
    public bool IgnoreErrors { get; set; } = true;
}

public class PerformanceOptions {
    /// <summary>
    /// Enables SIMD (Single Instruction, Multiple Data) optimizations for hashing and bit operations if the CPU supports it.
    /// Default: true.
    /// </summary>
    public bool EnableSimd { get; set; } = true;

    /// <summary>
    /// Global seed for the hash functions. Changing this invalidates all existing storage files.
    /// Default: 0.
    /// </summary>
    public long GlobalHashSeed { get; set; } = 0;
}

public class LifecycleOptions {
    /// <summary>
    /// Interval for the auto-save background job.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// If true, performs checksum validation when loading filters from disk.
    /// Default: true.
    /// </summary>
    public bool EnableIntegrityCheck { get; set; } = true;

    /// <summary>
    /// If true, loads all filters into RAM during application startup.
    /// Default: true.
    /// </summary>
    public bool EnableWarmUp { get; set; } = true;

    /// <summary>
    /// If true, automatically triggers registered seeders if data is missing or corrupted.
    /// Default: true.
    /// </summary>
    public bool AutoReseed { get; set; } = true;

    /// <summary>
    /// Threshold in bytes to automatically split a filter into shards.
    /// Default: 100 MB.
    /// </summary>
    public long ShardingThresholdBytes { get; set; } = 100 * 1024 * 1024;

    public bool AutoResetOnMismatch { get; set; } = true;
}

public class FilterDefinition {
    public long ExpectedItems { get; set; }
    public double ErrorRate { get; set; }
}