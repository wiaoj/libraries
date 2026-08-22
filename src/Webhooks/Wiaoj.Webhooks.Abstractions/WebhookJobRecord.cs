namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the persistent entity and execution state of a webhook job.
/// </summary>
public sealed class WebhookJobRecord {
    private readonly List<WebhookDeliveryAttempt> _attempts;

    /// <summary>
    /// Gets the unique identifier of the webhook job.
    /// </summary>
    public WebhookJobId Id { get; }

    /// <summary>
    /// Gets the target endpoint identifier.
    /// </summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>
    /// Gets the event type name or discriminator.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the pre-serialized event payload (typically JSON).
    /// </summary>
    public string SerializedPayload { get; }

    /// <summary>
    /// Gets the current lifecycle execution status of the job.
    /// </summary>
    public WebhookJobStatus Status { get; set; }

    /// <summary>
    /// Gets the timestamp when the job was initially created and queued.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets the timestamp when the next execution attempt should occur.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets the instance/pod identifier that currently holds the execution lease on this job.
    /// </summary>
    public string? LockedBy { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp of the active lease.
    /// </summary>
    public DateTimeOffset? LockExpiresAt { get; set; }

    /// <summary>
    /// Gets the chronological list of all delivery attempts executed for this job.
    /// </summary>
    public IReadOnlyList<WebhookDeliveryAttempt> Attempts => this._attempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookJobRecord"/> class.
    /// </summary>
    /// <param name="id">The unique job identifier.</param>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="eventType">The type name of the webhook event.</param>
    /// <param name="serializedPayload">The serialized payload content.</param>
    /// <param name="createdAt">The timestamp when the job was created.</param>
    public WebhookJobRecord(
        WebhookJobId id,
        WebhookEndpointId endpointId,
        string eventType,
        string serializedPayload,
        DateTimeOffset createdAt) {
        Preca.ThrowIfNullOrWhiteSpace(eventType);
        Preca.ThrowIfNull(serializedPayload);

        this.Id = id;
        this.EndpointId = endpointId;
        this.EventType = eventType;
        this.SerializedPayload = serializedPayload;
        this.Status = WebhookJobStatus.Queued;
        this.CreatedAt = createdAt;
        this._attempts = [];
    }

    /// <summary>
    /// Appends a new delivery attempt outcome to the job's audit history.
    /// </summary>
    /// <param name="attempt">The delivery attempt details.</param>
    public void AddAttempt(WebhookDeliveryAttempt attempt) {
        Preca.ThrowIfNull(attempt);
        this._attempts.Add(attempt);
    }
}
