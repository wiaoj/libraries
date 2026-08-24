namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents a persistent parent batch entity tracking 1-to-N fan-out progress across target endpoints.
/// </summary>
public sealed class WebhookPublishBatchRecord {
    /// <summary>Gets the unique batch identifier.</summary>
    public WebhookBatchId Id { get; }

    /// <summary>Gets the logical isolation namespace under which the batch was published.</summary>
    public WebhookNamespace Namespace { get; }

    /// <summary>Gets the canonical event discriminator name.</summary>
    public string EventName { get; }

    /// <summary>Gets the pre-serialized event payload.</summary>
    public string SerializedPayload { get; }

    /// <summary>Gets the list of target endpoint identifiers that must receive the event.</summary>
    public IReadOnlyList<WebhookEndpointId> TargetEndpoints { get; }

    /// <summary>Gets the total number of target subscriber endpoints.</summary>
    public int TargetSubscriberCount => this.TargetEndpoints.Count;

    /// <summary>Gets or sets the number of subscriber endpoints successfully dispatched so far.</summary>
    public int DispatchedCount { get; set; }

    /// <summary>Gets or sets the current lifecycle status of the batch.</summary>
    public WebhookBatchStatus Status { get; set; }

    /// <summary>Gets or sets the instance or pod identifier holding the recovery lease.</summary>
    public string? LockedBy { get; set; }

    /// <summary>Gets or sets the lease expiration timestamp.</summary>
    public DateTimeOffset? LockExpiresAt { get; set; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets or sets the last updated timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public WebhookPublishBatchRecord(
        WebhookBatchId id,
        WebhookNamespace @namespace,
        string eventName,
        string serializedPayload,
        IReadOnlyList<WebhookEndpointId> targetEndpoints,
        DateTimeOffset createdAt) {

        Preca.ThrowIfNullOrWhiteSpace(eventName);
        Preca.ThrowIfNull(serializedPayload);
        Preca.ThrowIfNull(targetEndpoints);

        this.Id = id;
        this.Namespace = @namespace;
        this.EventName = eventName;
        this.SerializedPayload = serializedPayload;
        this.TargetEndpoints = targetEndpoints;
        this.DispatchedCount = 0;
        this.Status = WebhookBatchStatus.InFlight;
        this.CreatedAt = createdAt;
        this.UpdatedAt = createdAt;
    }
}