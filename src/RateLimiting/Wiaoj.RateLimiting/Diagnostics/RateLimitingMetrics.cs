using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Central metrics provider for rate limiting operations.
/// </summary>
internal static class RateLimitingMetrics {
    public const string MeterName = "Wiaoj.RateLimiting";

    private static readonly string MeterVersion =
        typeof(RateLimitingMetrics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(RateLimitingMetrics).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

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

    public static void RecordDecision(string policy, string algorithm, bool isAllowed, int cost) {
        if(!DecisionsCounter.Enabled && !CostCounter.Enabled) {
            return;
        }

        TagList tags = new() {
            { "policy", policy },
            { "algorithm", algorithm },
            { "decision", isAllowed ? "allowed" : "denied" }
        };

        DecisionsCounter.Add(1, tags);

        if(isAllowed && CostCounter.Enabled) {
            TagList costTags = new() {
                { "policy", policy },
                { "algorithm", algorithm }
            };
            CostCounter.Add(cost, costTags);
        }
    }

    public static void RecordQueueWait(string policy, string algorithm, double milliseconds) {
        if(!QueueWaitDuration.Enabled) {
            return;
        }

        TagList tags = new() {
            { "policy", policy },
            { "algorithm", algorithm }
        };

        QueueWaitDuration.Record(milliseconds, tags);
    }
}