using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks;

/// <summary>
/// Root configuration options for the Wiaoj Webhooks core engine, node identity, and distributed coordination.
/// </summary>
public sealed class WebhookOptions {
    /// <summary>
    /// Gets or sets the globally unique instance or pod identifier used across all distributed lease locks,
    /// stale job recovery sweeps, and audit tracking.
    /// Automatically initialized with machine and process metadata if not explicitly configured.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when attempting to set a <see langword="null"/>, empty, or whitespace value.</exception>
    public string InstanceId {
        get;
        set {
            Preca.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = WebhookInstanceId.Resolve("node");
}