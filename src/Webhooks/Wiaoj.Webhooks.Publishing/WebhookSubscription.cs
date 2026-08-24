namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents a registered webhook subscription that routes matching domain events to a destination endpoint.
/// </summary>
public sealed class WebhookSubscription {
    /// <summary>
    /// Gets the unique subscription identifier.
    /// </summary>
    public WebhookSubscriptionId Id { get; }

    /// <summary>
    /// Gets the target destination endpoint identifier.
    /// </summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>
    /// Gets the event type discriminator pattern (e.g. <c>"order.created"</c>, <c>"order.*"</c>, <c>"*"</c>).
    /// </summary>
    public string EventTypePattern { get; }

    /// <summary>
    /// Gets or sets an optional content-based filter expression (e.g. JSONPath or property criteria).
    /// </summary>
    public string? FilterExpression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this subscription is actively receiving events. Default is <see langword="true"/>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the timestamp when this subscription was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets an optional human-readable description or tenant metadata.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Initializes a new subscription with an auto-generated identifier and the current UTC timestamp.
    /// </summary>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventTypePattern"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookSubscription(WebhookEndpointId endpointId, string eventTypePattern)
        : this(WebhookSubscriptionId.NewId(), endpointId, eventTypePattern, DateTimeOffset.UtcNow) {
    }

    /// <summary>
    /// Initializes a new subscription with an explicit identifier, destination endpoint, event discriminator pattern, and the current UTC timestamp.
    /// </summary>
    /// <param name="id">The unique subscription identifier.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against (e.g. <c>"order.created"</c>, <c>"order.*"</c>, <c>"*"</c>).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventTypePattern"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookSubscription(WebhookSubscriptionId id, WebhookEndpointId endpointId, string eventTypePattern)
        : this(id, endpointId, eventTypePattern, DateTimeOffset.UtcNow) {
    }

    /// <summary>
    /// Initializes a new subscription with an explicit identifier, endpoint, event pattern, and creation timestamp.
    /// </summary>
    /// <param name="id">The unique subscription identifier.</param>
    /// <param name="endpointId">The target destination endpoint identifier.</param>
    /// <param name="eventTypePattern">The event discriminator pattern to match against.</param>
    /// <param name="createdAt">The timestamp when the subscription was originally created.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventTypePattern"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookSubscription(WebhookSubscriptionId id,
                               WebhookEndpointId endpointId,
                               string eventTypePattern,
                               DateTimeOffset createdAt) {

        Preca.ThrowIfNullOrWhiteSpace(eventTypePattern);

        this.Id = id;
        this.EndpointId = endpointId;
        this.EventTypePattern = eventTypePattern;
        this.CreatedAt = createdAt;
    }
}