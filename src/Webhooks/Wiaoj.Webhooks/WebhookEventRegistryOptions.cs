namespace Wiaoj.Webhooks;

/// <summary>
/// Configuration options for webhook event type topology, explicit naming, and startup validation.
/// </summary>
public sealed class WebhookEventRegistryOptions {
    internal Dictionary<Type, string> Mappings { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether strict explicit event naming is enforced.
    /// When <see langword="true"/>, throws an exception at startup if any event relies on fallback class name conventions without an explicit name.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool EnforceExplicitNames { get; set; } = false;

    /// <summary>
    /// Explicitly maps an event type to a canonical wire-format discriminator name.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventName">The canonical wire-format name (e.g., <c>"order.created"</c>).</param>
    /// <returns>This options instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookEventRegistryOptions MapEvent<TEvent>(string eventName) where TEvent : IWebhookEvent {
        Preca.ThrowIfNullOrWhiteSpace(eventName);
        this.Mappings[typeof(TEvent)] = eventName;
        return this;
    }
}