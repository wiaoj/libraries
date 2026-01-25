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
    public static readonly ActivitySource ActivitySource = new(ServiceName, Version);

    // --- Metrics ---
    public static readonly Meter Meter = new(ServiceName, Version);

    // Counters
    public static readonly Counter<long> LookupCounter = Meter.CreateCounter<long>(
        "bloom_filter.lookups", "Number of lookups performed", "lookup");

    public static readonly Counter<long> HitCounter = Meter.CreateCounter<long>(
        "bloom_filter.hits", "Number of positive lookup results", "hit");

    // Histograms (Latency)
    public static readonly Histogram<double> Latency = Meter.CreateHistogram<double>(
        "bloom_filter.operation_latency", "Latency of filter operations", "ms");

    // Observables (Gauges)
    // These are initialized by the Service to report current state periodically
    public static ObservableGauge<double>? FillRatioGauge;
    public static ObservableGauge<double>? FalsePositiveGauge;
}