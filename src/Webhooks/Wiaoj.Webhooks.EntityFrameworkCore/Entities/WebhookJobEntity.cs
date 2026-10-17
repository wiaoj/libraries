namespace Wiaoj.Webhooks.EntityFrameworkCore;

/// <summary>
/// Row shape of a <see cref="WebhookJobRecord"/> in the jobs table.
/// </summary>
/// <remarks>
/// Timestamps are stored as UTC ticks (<see cref="DateTimeOffset.UtcTicks"/>) so that lease and recovery comparisons
/// translate to plain integer comparisons on every provider, SQLite included. The store converts to and from
/// <see cref="WebhookJobRecord"/>; application code reads jobs through <see cref="IWebhookStore"/>.
/// </remarks>
public sealed class WebhookJobEntity {
    /// <summary>Gets or sets the job identifier (<see cref="WebhookJobRecord.Id"/>).</summary>
    public string Id { get; set; } = null!;

    /// <summary>Gets or sets the batch identifier (<see cref="WebhookJobRecord.BatchId"/>).</summary>
    public string? BatchId { get; set; }

    /// <summary>Gets or sets the target endpoint identifier (<see cref="WebhookJobRecord.EndpointId"/>).</summary>
    public string EndpointId { get; set; } = null!;

    /// <summary>Gets or sets the partition key (<see cref="WebhookJobRecord.PartitionKey"/>).</summary>
    public string PartitionKey { get; set; } = null!;

    /// <summary>Gets or sets the event type (<see cref="WebhookJobRecord.EventType"/>).</summary>
    public string EventType { get; set; } = null!;

    /// <summary>Gets or sets the serialized payload (<see cref="WebhookJobRecord.SerializedPayload"/>).</summary>
    public string SerializedPayload { get; set; } = null!;

    /// <summary>Gets or sets the lifecycle status (<see cref="WebhookJobRecord.Status"/>).</summary>
    public WebhookJobStatus Status { get; set; }

    /// <summary>Gets or sets the creation time in UTC ticks (<see cref="WebhookJobRecord.CreatedAt"/>).</summary>
    public long CreatedAtTicks { get; set; }

    /// <summary>Gets or sets the next scheduled attempt in UTC ticks (<see cref="WebhookJobRecord.NextAttemptAt"/>).</summary>
    public long? NextAttemptAtTicks { get; set; }

    /// <summary>Gets or sets the lease holder (<see cref="WebhookJobRecord.LockedBy"/>).</summary>
    public string? LockedBy { get; set; }

    /// <summary>Gets or sets the lease expiry in UTC ticks (<see cref="WebhookJobRecord.LockExpiresAt"/>).</summary>
    public long? LockExpiresAtTicks { get; set; }

    /// <summary>Gets or sets the recorded delivery attempts (<see cref="WebhookJobRecord.Attempts"/>).</summary>
    public List<WebhookDeliveryAttemptEntity> Attempts { get; set; } = [];
}
