namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a unit of work handed off to an <see cref="IWebhookTransport"/> for delivery.
/// </summary>
public sealed record WebhookDeliveryJob {
    /// <summary>
    /// Gets the unique identifier of the webhook job.
    /// </summary>
    public WebhookJobId Id { get; }

    /// <summary>
    /// Gets the identifier of the endpoint this job should be delivered to.
    /// </summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>
    /// Gets the webhook event being delivered.
    /// </summary>
    public IWebhookEvent Payload { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryJob"/> record with an auto-generated job identifier.
    /// </summary>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="payload">The webhook event payload.</param>
    public WebhookDeliveryJob(WebhookEndpointId endpointId, IWebhookEvent payload)
        : this(WebhookJobId.NewJobId(), endpointId, payload) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryJob"/> record.
    /// </summary>
    /// <param name="id">The unique job identifier.</param>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="payload">The webhook event payload.</param>
    public WebhookDeliveryJob(WebhookJobId id, WebhookEndpointId endpointId, IWebhookEvent payload) {
        Preca.ThrowIfNull(payload);
        this.Id = id;
        this.EndpointId = endpointId;
        this.Payload = payload;
    }
}