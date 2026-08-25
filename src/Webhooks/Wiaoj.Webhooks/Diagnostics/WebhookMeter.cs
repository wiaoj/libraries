using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.Webhooks.Diagnostics;

/// <summary>
/// Central <see cref="Meter"/> and instruments for the Wiaoj Webhooks engine.
/// Uses <c>System.Diagnostics.Metrics</c> — compatible with OpenTelemetry, dotnet-counters, Prometheus, etc.
/// </summary>
internal static class WebhookMeter {
    /// <summary>
    /// The meter name used for subscribing in metrics configurations.
    /// </summary>
    public const string Name = "Wiaoj.Webhooks";

    /// <summary>
    /// The version of the meter assembly.
    /// </summary>
    public static readonly string Version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "1.0.0";

    private static readonly Meter _meter = new(Name, Version);

    // ── Dispatch Metrics ──────────────────────────────────────────────────────

    /// <summary>Total number of dispatched webhook events.</summary>
    public static readonly Counter<long> DispatchedEventsCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.dispatch.count",
            unit: "{events}",
            description: "Total number of webhook events dispatched to the transport.");

    /// <summary>Total number of failed webhook event dispatches.</summary>
    public static readonly Counter<long> DispatchErrorCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.dispatch.error.count",
            unit: "{errors}",
            description: "Total number of failed webhook event dispatches.");

    /// <summary>Total number of batch dispatch operations executed.</summary>
    public static readonly Counter<long> BatchDispatchCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.dispatch.batch.count",
            unit: "{batches}",
            description: "Total number of batch dispatch operations executed.");

    /// <summary>Distribution of event counts per batch dispatch operation.</summary>
    public static readonly Histogram<int> BatchSizeHistogram =
        _meter.CreateHistogram<int>(
            "wiaoj.webhooks.dispatch.batch.size",
            unit: "{events}",
            description: "Distribution of event counts contained in batch dispatches.");

    // ── Delivery Metrics ──────────────────────────────────────────────────────

    /// <summary>Total number of webhook delivery attempts.</summary>
    public static readonly Counter<long> DeliveryAttemptCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.delivery.attempt.count",
            unit: "{attempts}",
            description: "Total number of webhook delivery attempts executed.");

    /// <summary>Total number of successful webhook delivery attempts.</summary>
    public static readonly Counter<long> DeliverySuccessCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.delivery.success.count",
            unit: "{deliveries}",
            description: "Total number of successful webhook deliveries.");

    /// <summary>Total number of failed webhook delivery attempts.</summary>
    public static readonly Counter<long> DeliveryFailureCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.delivery.failure.count",
            unit: "{failures}",
            description: "Total number of failed webhook deliveries.");

    /// <summary>Duration of webhook delivery pipeline execution in milliseconds.</summary>
    public static readonly Histogram<double> DeliveryDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.webhooks.delivery.duration",
            unit: "ms",
            description: "Duration of webhook delivery pipeline execution in milliseconds.");

    /// <summary>Duration of raw HTTP requests in milliseconds.</summary>
    public static readonly Histogram<double> HttpRequestDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.webhooks.http.request.duration",
            unit: "ms",
            description: "Duration of raw HTTP requests in milliseconds.");

    /// <summary>Total number of webhook retry scheduling events.</summary>
    public static readonly Counter<long> RetryCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.retry.count",
            unit: "{retries}",
            description: "Total number of webhook retry attempts scheduled.");

    /// <summary>Total number of webhooks that exhausted all retries and were abandoned (dead letter).</summary>
    public static readonly Counter<long> DeadLetterCount =
        _meter.CreateCounter<long>(
            "wiaoj.webhooks.dead_letter.count",
            unit: "{dead_letters}",
            description: "Total number of webhook events that exceeded maximum retry attempts.");
}
