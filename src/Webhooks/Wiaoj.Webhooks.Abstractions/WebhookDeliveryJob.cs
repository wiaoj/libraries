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

    /// <summary>Initializes a new job with an auto-generated ID defaulting partition key to <paramref name="endpointId"/>.</summary>
    public WebhookDeliveryJob(WebhookEndpointId endpointId, string eventType, IWebhookEvent payload)
        : this(WebhookJobId.NewJobId(), endpointId, WebhookPartitionKey.From(endpointId), eventType, payload) {
    }

    /// <summary>Initializes a new job with an auto-generated ID and explicit partition key.</summary>
    public WebhookDeliveryJob(WebhookEndpointId endpointId, WebhookPartitionKey partitionKey, string eventType, IWebhookEvent payload)
        : this(WebhookJobId.NewJobId(), endpointId, partitionKey, eventType, payload) {
    }

    /// <summary>Initializes a new job with explicit ID defaulting partition key to <paramref name="endpointId"/>.</summary>
    public WebhookDeliveryJob(WebhookJobId id, WebhookEndpointId endpointId, string eventType, IWebhookEvent payload)
        : this(id, endpointId, WebhookPartitionKey.From(endpointId), eventType, payload) {
    }

    /// <summary>Initializes a new job with explicit ID, endpoint, partition key, event name, and payload.</summary>
    public WebhookDeliveryJob(WebhookJobId id, WebhookEndpointId endpointId, WebhookPartitionKey partitionKey, string eventType, IWebhookEvent payload) {
        Preca.ThrowIfNullOrWhiteSpace(partitionKey.Value);
        Preca.ThrowIfNullOrWhiteSpace(eventType);
        Preca.ThrowIfNull(payload);

        this.Id = id;
        this.EndpointId = endpointId;
        this.PartitionKey = partitionKey;
        this.EventType = eventType;
        this.Payload = payload;
    }
}