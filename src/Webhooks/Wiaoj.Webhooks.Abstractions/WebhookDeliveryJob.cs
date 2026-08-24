namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a persistent, self-contained unit of work handed off to an <see cref="IWebhookTransport"/> for asynchronous processing.
/// </summary>
public sealed record WebhookDeliveryJob {
    /// <summary>Gets the unique identifier of the webhook job.</summary>
    public WebhookJobId Id { get; }

    /// <summary>Gets the identifier of the target endpoint where this job will be delivered.</summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>Gets the partition routing key used for FIFO message ordering.</summary>
    public WebhookPartitionKey PartitionKey { get; }

    /// <summary>Gets the canonical wire-format event discriminator name (e.g., <c>"order.created"</c>).</summary>
    public string EventType { get; }

    /// <summary>Gets the webhook event domain payload being delivered.</summary>
    public IWebhookEvent Payload { get; }

    /// <summary>
    /// Gets a value indicating whether this job represents a manual replay of a previously delivered or failed job.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool IsReplay { get; init; } = false;


    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryJob"/> record with explicit routing, metadata, and domain payload details.
    /// </summary>
    /// <param name="id">The unique identifier of the delivery job.</param>
    /// <param name="endpointId">The destination endpoint identifier where the delivery will be directed.</param>
    /// <param name="partitionKey">The partition routing key used for strict FIFO message sequencing across queues and delivery locks.</param>
    /// <param name="eventType">The canonical wire-format event discriminator name (e.g., <c>"order.created"</c>).</param>
    /// <param name="payload">The domain event payload instance to be delivered.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="partitionKey"/> or <paramref name="eventType"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> is <see langword="null"/>.</exception>
    public WebhookDeliveryJob(
        WebhookJobId id,
        WebhookEndpointId endpointId,
        WebhookPartitionKey partitionKey,
        string eventType,
        IWebhookEvent payload) {

        Preca.ThrowIfNullOrWhiteSpace(partitionKey.Value);
        Preca.ThrowIfNullOrWhiteSpace(eventType);
        Preca.ThrowIfNull(payload);

        this.Id = id;
        this.EndpointId = endpointId;
        this.PartitionKey = partitionKey;
        this.EventType = eventType;
        this.Payload = payload;
    }

    /// <summary>
    /// Creates a new delivery job with an auto-generated time-ordered identifier (<see cref="WebhookJobId"/>),
    /// defaulting the partition key directly to the target <paramref name="endpointId"/>.
    /// </summary>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="eventType">The canonical wire-format event discriminator name.</param>
    /// <param name="payload">The domain event payload instance.</param>
    /// <returns>A new, fully populated <see cref="WebhookDeliveryJob"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> is <see langword="null"/>.</exception>
    public static WebhookDeliveryJob CreateDefault(WebhookEndpointId endpointId,
                                                   string eventType,
                                                   IWebhookEvent payload) {
        return new(WebhookJobId.NewJobId(), endpointId, WebhookPartitionKey.From(endpointId), eventType, payload);
    }

    /// <summary>
    /// Materializes a delivery job directly from an active <see cref="WebhookDeliveryContext"/>,
    /// ensuring complete preservation of job identity, endpoint details, and custom partition keys (e.g., during retries).
    /// </summary>
    /// <param name="context">The active webhook delivery execution context.</param>
    /// <returns>A <see cref="WebhookDeliveryJob"/> mirroring all routing metadata and payload state from the context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static WebhookDeliveryJob FromContext(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return new(context.JobId, context.Endpoint.Id, context.PartitionKey, context.EventType, context.Event) {
            IsReplay = context.IsReplay()
        };
    }
}