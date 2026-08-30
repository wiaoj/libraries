using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.Webhooks.AspNetCore.Diagnostics;

/// <summary>
/// Central <see cref="Meter"/> and instruments for the Wiaoj Webhooks Inbound Receiver engine.
/// </summary>
internal static class WebhookInboundMeter {
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

    /// <summary>Total number of inbound webhook requests received.</summary>
    public static readonly Counter<long> InboundRequestCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.inbound.requests",
            unit: "{requests}",
            description: "Total number of inbound webhook requests received.");

    /// <summary>Total duration of inbound webhook processing in milliseconds.</summary>
    public static readonly Histogram<double> InboundDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.webhooks.inbound.duration",
            unit: "ms",
            description: "Total duration of inbound webhook processing in milliseconds.");

    /// <summary>Total number of inbound webhooks rejected due to invalid cryptographic signature.</summary>
    public static readonly Counter<long> SignatureFailedCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.inbound.signature_failed.count",
            unit: "{failures}",
            description: "Total number of inbound webhooks rejected due to invalid cryptographic signature.");

    /// <summary>Total number of inbound webhooks rejected due to expired timestamp / replay attack.</summary>
    public static readonly Counter<long> TimestampExpiredCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.inbound.timestamp_expired.count",
            unit: "{rejections}",
            description: "Total number of inbound webhooks rejected due to expired timestamp or replay attack.");

    /// <summary>Total number of inbound webhooks rejected due to hop limit or causal loop detection.</summary>
    public static readonly Counter<long> LoopDetectedCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.inbound.loop_detected.count",
            unit: "{loops}",
            description: "Total number of inbound webhooks rejected due to hop limit or causal loop detection.");
}
