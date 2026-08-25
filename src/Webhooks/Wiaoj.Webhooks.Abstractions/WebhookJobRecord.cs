using Wiaoj.Abstractions;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the persistent entity and execution state of a webhook job.
/// </summary>
/// <remarks>
/// Instances of this class are mutable. Callers must ensure exclusive access
/// (e.g. via an endpoint lock or transaction) when updating state via <see cref="CopyFrom"/>,
/// as in-place mutations across multiple fields are not atomic.
/// </remarks>
public sealed class WebhookJobRecord : ICopyFrom<WebhookJobRecord> {
    private readonly List<WebhookDeliveryAttempt> _attempts;

    /// <summary>Gets the unique identifier of the webhook job.</summary>
    public WebhookJobId Id { get; }

    /// <summary>Gets the optional batch identifier if this job was created as part of a bulk dispatch operation.</summary>
    public string? BatchId { get; init; }

    /// <summary>Gets the target endpoint identifier.</summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>Gets the partition routing key used for FIFO ordering and database index partitioning.</summary>
    public string PartitionKey { get; }

    /// <summary>Gets the event type name or discriminator.</summary>
    public string EventType { get; }

    /// <summary>Gets the pre-serialized event payload (typically JSON).</summary>
    public string SerializedPayload { get; }

    /// <summary>Gets the current lifecycle execution status of the job.</summary>
    public WebhookJobStatus Status { get; set; }

    /// <summary>Gets the timestamp when the job was initially created and queued.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets or sets the timestamp when the next execution attempt should occur.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>Gets or sets the instance/pod identifier that currently holds the execution lease on this job.</summary>
    public string? LockedBy { get; set; }

    /// <summary>Gets or sets the expiration timestamp of the active lease.</summary>
    public DateTimeOffset? LockExpiresAt { get; set; }

    /// <summary>Gets the chronological list of all delivery attempts executed for this job.</summary>
    public IReadOnlyList<WebhookDeliveryAttempt> Attempts => this._attempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookJobRecord"/> class defaulting partition key to <paramref name="endpointId"/>.
    /// </summary>
    public WebhookJobRecord(
        WebhookJobId id,
        WebhookEndpointId endpointId,
        string eventType,
        string serializedPayload,
        DateTimeOffset createdAt)
        : this(id, endpointId, endpointId.Value, eventType, serializedPayload, createdAt) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookJobRecord"/> class with an explicit partition key.
    /// </summary>
    public WebhookJobRecord(
        WebhookJobId id,
        WebhookEndpointId endpointId,
        string partitionKey,
        string eventType,
        string serializedPayload,
        DateTimeOffset createdAt) {
        Preca.ThrowIfNullOrWhiteSpace(partitionKey);
        Preca.ThrowIfNullOrWhiteSpace(eventType);
        Preca.ThrowIfNull(serializedPayload);

        this.Id = id;
        this.EndpointId = endpointId;
        this.PartitionKey = partitionKey;
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

    /// <summary>
    /// Copies mutable lifecycle execution state from the specified source record into the current instance.
    /// </summary>
    /// <param name="source">The source record containing the updated state.</param>
    public void CopyFrom(WebhookJobRecord source) {
        Preca.ThrowIfNull(source);
        Preca.ThrowIf(
            !this.Id.Equals(source.Id),
            static () => new InvalidOperationException("CopyFrom can only be performed between job records sharing the exact same JobId."));

        this.Status = source.Status;
        this.NextAttemptAt = source.NextAttemptAt;
        this.LockedBy = source.LockedBy;
        this.LockExpiresAt = source.LockExpiresAt;

        this._attempts.Clear();
        this._attempts.AddRange(source.Attempts);
    }
}