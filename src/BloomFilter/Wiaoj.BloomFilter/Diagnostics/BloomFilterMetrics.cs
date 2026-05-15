using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Wiaoj.BloomFilter.Diagnostics;
/// <summary>
/// Provides instrumentation for Bloom Filter operations.
/// </summary>
public static class BloomFilterDiagnostics {
    private static readonly string Version = typeof(BloomFilterDiagnostics).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    private static readonly string ServiceName = "Wiaoj.BloomFilter";

    // --- Tracing ---
    /// <summary>
    /// The source for OpenTelemetry tracing activities related to Bloom Filters.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, Version);

    /// <summary>
    /// The meter used to publish Bloom Filter performance metrics.
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, Version);

    /// <summary>
    /// Counter tracking the total number of membership lookups performed.
    /// </summary>
    public static readonly Counter<long> LookupCounter = Meter.CreateCounter<long>(
        "bloom_filter.lookups", "Number of lookups performed", "lookup");

    /// <summary>
    /// Counter tracking the number of lookups that returned a positive (potential match) result.
    /// </summary>
    public static readonly Counter<long> HitCounter = Meter.CreateCounter<long>(
        "bloom_filter.hits", "Number of positive lookup results", "hit");

    /// <summary>
    /// Histogram tracking the execution latency of filter operations in milliseconds.
    /// </summary>
    public static readonly Histogram<double> Latency = Meter.CreateHistogram<double>(
        "bloom_filter.operation_latency", "Latency of filter operations", "ms");

    /// <summary>
    /// Gauge reporting the current fill ratio (saturation) of the active filters.
    /// </summary>
    public static ObservableGauge<double>? FillRatioGauge;

    /// <summary>
    /// Gauge reporting the estimated false positive probability of the active filters.
    /// </summary>
    public static ObservableGauge<double>? FalsePositiveGauge;
}