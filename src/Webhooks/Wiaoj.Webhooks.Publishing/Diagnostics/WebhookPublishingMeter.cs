using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.Webhooks.Publishing.Diagnostics;

/// <summary>
/// Central <see cref="Meter"/> and instruments for the Wiaoj Webhooks Publishing engine.
/// </summary>
internal static class WebhookPublishingMeter {
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

    /// <summary>Total number of events published to subscribers.</summary>
    public static readonly Counter<long> PublishedEventsCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.publishing.published.count",
            unit: "{events}",
            description: "Total number of events published to subscribers.");

    /// <summary>Distribution of endpoint subscribers matched per published event.</summary>
    public static readonly Histogram<int> FanOutEndpointsHistogram =
        _meter.CreateHistogram<int>(
            "wiaoj.webhooks.publishing.fan_out.endpoints",
            unit: "{endpoints}",
            description: "Distribution of endpoint subscribers matched per published event.");
}
