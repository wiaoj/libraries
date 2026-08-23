namespace Wiaoj.Webhooks.AspNetCore;

/// <summary>
/// Root configuration options for the inbound webhook receiver engine.
/// </summary>
public sealed class WebhookInboundOptions {
    /// <summary>Gets the dictionary of configured named receiver policies.</summary>
    public Dictionary<string, WebhookReceiverPolicy> Policies { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the default baseline policy applied when no named policy is assigned.</summary>
    public WebhookReceiverPolicy DefaultPolicy { get; } = new() { Name = "Default" };
}