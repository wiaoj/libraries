using System.Diagnostics;

namespace Wiaoj.Webhooks.Publishing.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for distributed tracing across the event publishing engine.
/// </summary>
internal static class WebhookPublishingActivitySource {
    /// <summary>
    /// The unified activity source name used for distributed tracing.
    /// </summary>
    public const string Name = "Wiaoj.Webhooks";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance.
    /// </summary>
    public static readonly ActivitySource Instance = new(Name, "1.0.0");

    /// <summary>
    /// Starts a new activity representing an event publishing operation.
    /// </summary>
    /// <param name="eventName">The domain event name.</param>
    /// <param name="namespace">The target webhook namespace.</param>
    /// <returns>The started <see cref="Activity"/>, or <see langword="null"/> when nobody is listening.</returns>
    public static Activity? StartPublishActivity(string eventName, string @namespace) {
        Activity? activity = Instance.StartActivity("webhook.publish", ActivityKind.Producer);

        activity?.SetTag("webhook.event_name", eventName);
        activity?.SetTag("webhook.namespace", @namespace);

        return activity;
    }
}
