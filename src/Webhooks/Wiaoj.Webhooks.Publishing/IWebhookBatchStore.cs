namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Contract for persisting, updating, and recovering 1-to-N webhook publish batches.
/// </summary>
public interface IWebhookBatchStore {
    /// <summary>Persists a new parent publish batch before fan-out dispatching begins.</summary>
    ValueTask SaveBatchAsync(WebhookPublishBatchRecord batch, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a batch by its unique identifier.</summary>
    ValueTask<WebhookPublishBatchRecord?> GetBatchAsync(WebhookBatchId batchId, CancellationToken cancellationToken = default);

    /// <summary>Updates progress count and status of an active batch.</summary>
    ValueTask UpdateBatchProgressAsync(WebhookBatchId batchId, int dispatchedCount, WebhookBatchStatus status, CancellationToken cancellationToken = default);

    /// <summary>Attempts to claim an exclusive recovery lease on an in-flight batch.</summary>
    ValueTask<bool> TryClaimBatchLeaseAsync(WebhookBatchId batchId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>Retrieves stale in-flight batches whose recovery lease has expired.</summary>
    ValueTask<IReadOnlyList<WebhookPublishBatchRecord>> GetStaleInFlightBatchesAsync(DateTimeOffset threshold, int maxCount, CancellationToken cancellationToken = default);
}