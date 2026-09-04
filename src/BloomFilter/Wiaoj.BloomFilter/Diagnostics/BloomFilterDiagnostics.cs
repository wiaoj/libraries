using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Wiaoj.BloomFilter.Diagnostics;

/// <summary>
/// Internal instrumentation engine providing OpenTelemetry Tracing and Metrics for Bloom Filters.
/// </summary>
internal static class BloomFilterDiagnostics {
    public const string MeterName = "Wiaoj.BloomFilter";
    public const string ActivitySourceName = "Wiaoj.BloomFilter";
    private static readonly string Version = typeof(BloomFilterDiagnostics).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// The source for OpenTelemetry tracing activities (Spans).
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    // Standart Span İsimleri
    public const string ActivitySave = "BloomFilter.Save";
    public const string ActivityReload = "BloomFilter.Reload";
    public const string ActivitySeeding = "BloomFilter.Seeding";
    public const string ActivityWarmUp = "BloomFilter.WarmUp";
    public const string ActivityScaleUp = "BloomFilter.ScaleUp";

    // Standart Tag / Attribute İsimleri (Semantic Conventions)
    public const string TagFilterName = "bloom_filter.name";
    public const string TagSizeInBits = "bloom_filter.size_bits";
    public const string TagPopCount = "bloom_filter.pop_count";
    public const string TagChecksum = "bloom_filter.checksum";
    public const string TagBytesWritten = "bloom_filter.bytes_written";
    public const string TagItemsSeeded = "bloom_filter.items_seeded";
    public const string TagLayerIndex = "bloom_filter.layer_index";

    /// <summary>
    /// The meter used to publish high-performance Bloom Filter metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, Version);

    // --- Counters ---

    /// <summary>
    /// Tracks the total number of membership queries performed.
    /// </summary>
    public static readonly Counter<long> LookupCounter = Meter.CreateCounter<long>(
        "bloom_filter.lookups.total",
        unit: "{lookup}",
        description: "Total number of Contains lookups performed.");

    /// <summary>
    /// Tracks the number of lookups that returned true (potential match).
    /// </summary>
    public static readonly Counter<long> HitCounter = Meter.CreateCounter<long>(
        "bloom_filter.hits.total",
        unit: "{hit}",
        description: "Total number of positive (might contain) lookup results.");

    /// <summary>
    /// Tracks the total number of items inserted across all filters.
    /// </summary>
    public static readonly Counter<long> AddCounter = Meter.CreateCounter<long>(
        "bloom_filter.items_added.total",
        unit: "{item}",
        description: "Total number of Add operations performed.");

    /// <summary>
    /// Tracks the total persistent bytes written to disk/storage.
    /// </summary>
    public static readonly Counter<long> BytesWrittenCounter = Meter.CreateCounter<long>(
        "bloom_filter.storage.bytes_written.total",
        unit: "By",
        description: "Total volume of serialized snapshot data written to storage.");

    /// <summary>
    /// Tracks the total number of layer scale-ups in Scalable Bloom Filters.
    /// </summary>
    public static readonly Counter<long> ScalableLayerSpawnCounter = Meter.CreateCounter<long>(
        "bloom_filter.scalable.layers_spawned.total",
        unit: "{layer}",
        description: "Number of dynamic layers spawned due to saturation.");

    // --- Histograms (Latencies & Durations) ---

    /// <summary>
    /// Tracks the execution duration of Save snapshot operations in milliseconds.
    /// </summary>
    public static readonly Histogram<double> SaveDuration = Meter.CreateHistogram<double>(
        "bloom_filter.save.duration",
        unit: "ms",
        description: "Duration of filter persistence operations.");

    /// <summary>
    /// Tracks the execution duration of Reload operations in milliseconds.
    /// </summary>
    public static readonly Histogram<double> ReloadDuration = Meter.CreateHistogram<double>(
        "bloom_filter.reload.duration",
        unit: "ms",
        description: "Duration of loading and verifying filter snapshots from storage.");

    /// <summary>
    /// Tracks the total seeding duration in milliseconds.
    /// </summary>
    public static readonly Histogram<double> SeedingDuration = Meter.CreateHistogram<double>(
        "bloom_filter.seeding.duration",
        unit: "ms",
        description: "Duration of populating filters via external data seeders.");

}