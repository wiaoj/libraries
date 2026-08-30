using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.Webhooks.BloomFilter.Diagnostics;

/// <summary>
/// Central <see cref="Meter"/> and instruments for the Wiaoj Bloom Filter Webhook engine.
/// </summary>
internal static class BloomFilterMeter {
    /// <summary>
    /// The unified meter name used for subscribing in metrics configurations.
    /// </summary>
    public const string Name = "Wiaoj.Webhooks";

    /// <summary>
    /// The version of the meter assembly.
    /// </summary>
    public static readonly string Version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "1.0.0";

    private static readonly Meter _meter = new(Name, Version);

    /// <summary>Total number of duplicate webhook deliveries intercepted by the Bloom filter.</summary>
    public static readonly Counter<long> BloomFilterHitsCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.bloom_filter.hits.count",
            unit: "{hits}",
            description: "Total number of duplicate webhook deliveries intercepted by the Bloom filter.");
}
