using System.Diagnostics.Metrics;

namespace Wiaoj.DistributedCounter.Diagnostics;
internal static class DistributedCounterMetrics {
    // Meter adı kütüphane adı ile aynı olmalı
    public static readonly Meter Meter = new("Wiaoj.DistributedCounter", "1.0.0");

    // Sayaçlar
    private static readonly Counter<long> IncrementsCounter = Meter.CreateCounter<long>(
        "distributed_counter.increments",
        description: "Total number of increment operations requested.");

    private static readonly Counter<long> FlushesCounter = Meter.CreateCounter<long>(
        "distributed_counter.flushes",
        description: "Total number of buffer flush operations triggered.");

    // Histogram (Süre ölçümü için)
    private static readonly Histogram<double> FlushDurationHistogram = Meter.CreateHistogram<double>(
        "distributed_counter.flush_duration",
        unit: "ms",
        description: "Duration of flush operations to storage.");

    // Metotlar
    public static void RecordIncrement(string counterName, string strategy, long amount) {
        // Tag'ler sayesinde Grafana'da "Sadece 'Buffered' olanları göster" diyebilirsin.
        IncrementsCounter.Add(amount,
            new KeyValuePair<string, object?>("name", counterName),
            new KeyValuePair<string, object?>("strategy", strategy));
    }

    public static void RecordFlush() => FlushesCounter.Add(1);

    public static void RecordFlushDuration(double ms) => FlushDurationHistogram.Record(ms);
}