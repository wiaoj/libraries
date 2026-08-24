using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Central OpenTelemetry and .NET runtime metrics provider for <c>Wiaoj.RateLimiting</c>.
/// Exposes standard meters, counters, and histograms for Prometheus and OpenTelemetry
/// collectors with zero external package dependencies.
/// </summary>
public static class RateLimitingMetrics {
    /// <summary>
    /// The meter name used to subscribe to metrics via OpenTelemetry (<c>.AddMeter("Wiaoj.RateLimiting")</c>).
    /// </summary>
    public const string MeterName = "Wiaoj.RateLimiting";

    private static readonly string MeterVersion =
        typeof(RateLimitingMetrics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(RateLimitingMetrics).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    /// <summary>
    /// The shared <see cref="System.Diagnostics.Metrics.Meter"/> instance for this package.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, MeterVersion);

    private static readonly Counter<long> DecisionsCounter = Meter.CreateCounter<long>(
        name: "ratelimit.decisions",
        unit: "{decision}",
        description: "Number of rate limiting decisions made (allowed or denied).");

    private static readonly Counter<long> CostCounter = Meter.CreateCounter<long>(
        name: "ratelimit.cost.consumed",
        unit: "{unit}",
        description: "Total rate limiting cost/tokens consumed by allowed requests.");

    private static readonly Histogram<double> QueueWaitDuration = Meter.CreateHistogram<double>(
        name: "ratelimit.queue.wait_duration",
        unit: "ms",
        description: "Time in milliseconds requests spent waiting in traffic-shaping queues before execution.");

    /// <summary>
    /// Records a rate limit decision metric. Uses <see cref="TagList"/> to prevent heap allocations.
    /// </summary>
    /// <param name="algorithm">The name of the rate limiting algorithm.</param> 
    /// <param name="isAllowed">Whether the operation was permitted.</param>
    /// <param name="cost">The number of cost/token units requested.</param>
    public static void RecordDecision(string algorithm, bool isAllowed, int cost) {
        if(!DecisionsCounter.Enabled && !CostCounter.Enabled) {
            return;
        }

        TagList tags = new() {
            { "algorithm", algorithm },
            { "decision", isAllowed ? "allowed" : "denied" }
        };

        DecisionsCounter.Add(1, tags);

        if(isAllowed && CostCounter.Enabled) {
            TagList costTags = new() {
                { "algorithm", algorithm }
            };
            CostCounter.Add(cost, costTags);
        }
    }

    /// <summary>
    /// Records the time spent waiting in a traffic-shaping queue.
    /// </summary>
    /// <param name="algorithm">The name of the rate limiting algorithm.</param> 
    /// <param name="milliseconds">The duration waited in milliseconds.</param>
    public static void RecordQueueWait(string algorithm, double milliseconds) {
        if(!QueueWaitDuration.Enabled) {
            return;
        }

        TagList tags = new() {
            { "algorithm", algorithm }
        };

        QueueWaitDuration.Record(milliseconds, tags);
    }
}