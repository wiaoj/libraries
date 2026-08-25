namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents a registered webhook subscription that routes matching domain events to a destination endpoint within an isolated namespace.
/// </summary>
public sealed class WebhookSubscription {
    /// <summary>
    /// Gets the unique subscription identifier.
    /// </summary>
    public WebhookSubscriptionId Id { get; }

    /// <summary>
    /// Gets the logical isolation namespace or tenant boundary under which this subscription is registered.
    /// </summary>
    public WebhookNamespace Namespace { get; init; }

    /// <summary>
    /// Gets the target destination endpoint identifier.
    /// </summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>
    /// Gets the event type discriminator pattern (e.g. <c>"order.created"</c>, <c>"order.*"</c>, <c>"*"</c>).
    /// </summary>
    public string EventTypePattern { get; }

    /// <summary>
    /// Gets or sets an optional content-based filter expression.
    /// </summary>
    public string? FilterExpression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this subscription is actively receiving events.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets the timestamp when this subscription was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets an optional human-readable description or tenant metadata.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Initializes a new subscription in the default namespace with an auto-generated identifier.
    /// </summary>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    public WebhookSubscription(WebhookEndpointId endpointId, string eventTypePattern)
        : this(WebhookSubscriptionId.NewId(), WebhookNamespace.Default, endpointId, eventTypePattern, DateTimeOffset.UtcNow) {
    }

    /// <summary>
    /// Initializes a new subscription in the specified namespace with an auto-generated identifier.
    /// </summary>
    /// <param name="namespace">The isolation namespace.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    public WebhookSubscription(WebhookNamespace @namespace, WebhookEndpointId endpointId, string eventTypePattern)
        : this(WebhookSubscriptionId.NewId(), @namespace, endpointId, eventTypePattern, DateTimeOffset.UtcNow) {
    }

    /// <summary>
    /// Initializes a new subscription in the default namespace with an explicit identifier.
    /// </summary>
    /// <param name="id">The unique subscription identifier.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    public WebhookSubscription(WebhookSubscriptionId id, WebhookEndpointId endpointId, string eventTypePattern)
        : this(id, WebhookNamespace.Default, endpointId, eventTypePattern, DateTimeOffset.UtcNow) {
    }

    /// <summary>
    /// Initializes a new subscription in the specified namespace with an explicit identifier.
    /// </summary>
    /// <param name="id">The unique subscription identifier.</param>
    /// <param name="namespace">The isolation namespace.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    public WebhookSubscription(WebhookSubscriptionId id, WebhookNamespace @namespace, WebhookEndpointId endpointId, string eventTypePattern)
        : this(id, @namespace, endpointId, eventTypePattern, DateTimeOffset.UtcNow) {
    }

    /// <summary>
    /// Initializes a new subscription in the default namespace with an explicit identifier and creation timestamp.
    /// </summary>
    /// <param name="id">The unique subscription identifier.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    public WebhookSubscription(WebhookSubscriptionId id, WebhookEndpointId endpointId, string eventTypePattern, DateTimeOffset createdAt)
        : this(id, WebhookNamespace.Default, endpointId, eventTypePattern, createdAt) {
    }

    /// <summary>
    /// Initializes a new subscription with all required metadata parameters.
    /// </summary>
    /// <param name="id">The unique subscription identifier.</param>
    /// <param name="namespace">The isolation namespace.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    public WebhookSubscription(
        WebhookSubscriptionId id,
        WebhookNamespace @namespace,
        WebhookEndpointId endpointId,
        string eventTypePattern,
        DateTimeOffset createdAt) {

        Preca.ThrowIfNullOrWhiteSpace(eventTypePattern);

        this.Id = id;
        this.Namespace = @namespace;
        this.EndpointId = endpointId;
        this.EventTypePattern = eventTypePattern;
        this.CreatedAt = createdAt;
        this.IsEnabled = true;
    }
}