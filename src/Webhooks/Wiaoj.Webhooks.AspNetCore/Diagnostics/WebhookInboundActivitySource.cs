using System.Diagnostics;

namespace Wiaoj.Webhooks.AspNetCore.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for distributed tracing across the inbound webhook receiver engine.
/// </summary>
internal static class WebhookInboundActivitySource {
    /// <summary>
    /// The unified activity source name used for distributed tracing.
    /// </summary>
    public const string Name = "Wiaoj.Webhooks";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance.
    /// </summary>
    public static readonly ActivitySource Instance = new(Name, "1.0.0");

    /// <summary>
    /// Starts a new activity representing an inbound webhook request processing.
    /// </summary>
    /// <param name="policyName">The name of the inbound webhook policy.</param>
    /// <param name="path">The HTTP request path.</param>
    /// <returns>The started <see cref="Activity"/>, or <see langword="null"/> when nobody is listening.</returns>
    public static Activity? StartInboundActivity(string policyName, string path) {
        Activity? activity = Instance.StartActivity("webhook.inbound.receive", ActivityKind.Server);

        activity?.SetTag("webhook.policy", policyName);
        activity?.SetTag("http.route", path);

        return activity;
    }
}
