using System.Diagnostics;

namespace Wiaoj.Webhooks.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for distributed tracing across the webhook outbound pipeline.
/// </summary>
/// <remarks>
/// Consumers enable tracing by registering a listener (e.g. via OpenTelemetry's
/// <c>AddSource("Wiaoj.Webhooks")</c>). When no listener is registered, <see cref="StartDeliveryActivity"/>
/// returns <see langword="null"/> and tracing has effectively zero overhead — it never affects the
/// value recorded in <see cref="WebhookDeliveryAttempt.Duration"/>, which is measured independently.
/// </remarks>
internal static class WebhookActivitySource {
    private const string Name = "Wiaoj.Webhooks";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance used for all webhook delivery spans.
    /// </summary>
    public static readonly ActivitySource Instance = new(Name, "1.0.0");

    /// <summary>
    /// Starts a new activity representing an event dispatch, if a listener is registered.
    /// </summary>
    /// <param name="endpointId">The target endpoint identifier.</param>
    /// <param name="eventName">The name of the event being dispatched.</param>
    /// <returns>The started <see cref="Activity"/>, or <see langword="null"/> when nobody is listening.</returns>
    public static Activity? StartDispatchActivity(WebhookEndpointId endpointId, string eventName) {
        Activity? activity = Instance.StartActivity("webhook.dispatch", ActivityKind.Producer);

        activity?.SetTag("webhook.endpoint_id", endpointId.Value);
        activity?.SetTag("webhook.event_name", eventName);

        return activity;
    }

    /// <summary>
    /// Starts a new activity representing a single delivery attempt, if a listener is registered.
    /// </summary>
    /// <param name="context">The delivery context the activity describes.</param>
    /// <param name="attemptNumber">The one-based attempt number about to be executed.</param>
    /// <returns>The started <see cref="Activity"/>, or <see langword="null"/> when nobody is listening.</returns>
    public static Activity? StartDeliveryActivity(WebhookDeliveryContext context, int attemptNumber) {
        Activity? activity = Instance.StartActivity("webhook.deliver", ActivityKind.Client);

        activity?.SetTag("webhook.endpoint_id", context.Endpoint.Id.Value);
        activity?.SetTag("webhook.target_url", context.TargetUrl.ToString());
        activity?.SetTag("webhook.attempt_number", attemptNumber);

        return activity;
    }

    /// <summary>
    /// Starts a new activity representing an outbound HTTP delivery request.
    /// </summary>
    /// <param name="targetUrl">The target destination URL.</param>
    /// <returns>The started <see cref="Activity"/>, or <see langword="null"/> when nobody is listening.</returns>
    public static Activity? StartHttpActivity(Uri targetUrl) {
        Activity? activity = Instance.StartActivity("webhook.http.post", ActivityKind.Client);

        activity?.SetTag("http.request.method", "POST");
        activity?.SetTag("url.full", targetUrl.ToString());

        return activity;
    }
}