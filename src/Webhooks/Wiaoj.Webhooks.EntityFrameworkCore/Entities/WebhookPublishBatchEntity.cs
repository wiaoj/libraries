using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore;

/// <summary>
/// Row shape of a <see cref="WebhookPublishBatchRecord"/> in the publish batches table.
/// </summary>
public sealed class WebhookPublishBatchEntity {
    /// <summary>Gets or sets the batch identifier (<see cref="WebhookPublishBatchRecord.Id"/>).</summary>
    public string Id { get; set; } = null!;

    /// <summary>Gets or sets the isolation namespace (<see cref="WebhookPublishBatchRecord.Namespace"/>).</summary>
    public string Namespace { get; set; } = null!;

    /// <summary>Gets or sets the event name (<see cref="WebhookPublishBatchRecord.EventName"/>).</summary>
    public string EventName { get; set; } = null!;

    /// <summary>Gets or sets the serialized payload (<see cref="WebhookPublishBatchRecord.SerializedPayload"/>).</summary>
    public string SerializedPayload { get; set; } = null!;

    /// <summary>
    /// Gets or sets the target endpoints (<see cref="WebhookPublishBatchRecord.TargetEndpoints"/>) as a JSON array of
    /// strings. Their order is significant: <see cref="DispatchedCount"/> counts a prefix of it.
    /// </summary>
    public string TargetEndpoints { get; set; } = null!;

    /// <summary>Gets or sets the dispatched count (<see cref="WebhookPublishBatchRecord.DispatchedCount"/>).</summary>
    public int DispatchedCount { get; set; }

    /// <summary>Gets or sets the lifecycle status (<see cref="WebhookPublishBatchRecord.Status"/>).</summary>
    public WebhookBatchStatus Status { get; set; }

    /// <summary>Gets or sets the recovery lease holder (<see cref="WebhookPublishBatchRecord.LockedBy"/>).</summary>
    public string? LockedBy { get; set; }

    /// <summary>Gets or sets the lease expiry in UTC ticks (<see cref="WebhookPublishBatchRecord.LockExpiresAt"/>).</summary>
    public long? LockExpiresAtTicks { get; set; }

    /// <summary>Gets or sets the creation time in UTC ticks (<see cref="WebhookPublishBatchRecord.CreatedAt"/>).</summary>
    public long CreatedAtTicks { get; set; }

    /// <summary>Gets or sets the last update time in UTC ticks (<see cref="WebhookPublishBatchRecord.UpdatedAt"/>).</summary>
    public long UpdatedAtTicks { get; set; }
}
