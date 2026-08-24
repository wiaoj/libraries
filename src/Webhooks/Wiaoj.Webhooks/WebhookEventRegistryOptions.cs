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
    /// Initializes a new instance of the <see cref="WebhookEventRegistryOptions"/> class.
    /// </summary>
    public WebhookEventRegistryOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEventRegistryOptions"/> class with predefined event type mappings.
    /// </summary>
    /// <param name="mappings">A parameter array of event type and wire-format discriminator name pairs.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mappings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any event type does not implement <see cref="IWebhookEvent"/> or any event name is invalid.</exception>
    public WebhookEventRegistryOptions(params (Type EventType, string EventName)[] mappings) {
        Preca.ThrowIfNull(mappings);

        for(int i = 0; i < mappings.Length; i++) {
            MapEvent(mappings[i].EventType, mappings[i].EventName);
        }
    }

    /// <summary>
    /// Explicitly maps an event type to a canonical wire-format discriminator name.
    /// </summary>
    /// <typeparam name="TEvent">The event type implementing <see cref="IWebhookEvent"/>.</typeparam>
    /// <param name="eventName">The canonical wire-format name (e.g., <c>"order.created"</c>).</param>
    /// <returns>This options instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookEventRegistryOptions MapEvent<TEvent>(string eventName) where TEvent : IWebhookEvent {
        return MapEvent(typeof(TEvent), eventName);
    }

    /// <summary>
    /// Explicitly maps a non-generic event type to a canonical wire-format discriminator name.
    /// </summary>
    /// <param name="eventType">The event CLR type implementing <see cref="IWebhookEvent"/>.</param>
    /// <param name="eventName">The canonical wire-format name (e.g., <c>"order.created"</c>).</param>
    /// <returns>This options instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="eventType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> does not implement <see cref="IWebhookEvent"/> or <paramref name="eventName"/> is invalid.</exception>
    public WebhookEventRegistryOptions MapEvent(Type eventType, string eventName) {
        Preca.ThrowIfNull(eventType);
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        if(!typeof(IWebhookEvent).IsAssignableFrom(eventType)) {
            throw new ArgumentException($"Type '{eventType.FullName}' must implement '{nameof(IWebhookEvent)}'.", nameof(eventType));
        }

        this.Mappings[eventType] = eventName;
        return this;
    }
}