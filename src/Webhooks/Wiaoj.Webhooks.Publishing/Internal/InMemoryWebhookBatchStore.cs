using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// Thread-safe in-memory store for tracking and testing 1-to-N publish batches.
/// </summary>
internal sealed class InMemoryWebhookBatchStore : IWebhookBatchStore {
    private readonly ConcurrentDictionary<WebhookBatchId, WebhookPublishBatchRecord> _batches = new();

    public ValueTask SaveBatchAsync(WebhookPublishBatchRecord batch, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(batch);
        this._batches[batch.Id] = batch;
        return ValueTask.CompletedTask;
    }

    public ValueTask<WebhookPublishBatchRecord?> GetBatchAsync(WebhookBatchId batchId, CancellationToken cancellationToken = default) {
        this._batches.TryGetValue(batchId, out WebhookPublishBatchRecord? batch);
        return ValueTask.FromResult(batch);
    }

    public ValueTask UpdateBatchProgressAsync(WebhookBatchId batchId, int dispatchedCount, WebhookBatchStatus status, CancellationToken cancellationToken = default) {
        if(this._batches.TryGetValue(batchId, out WebhookPublishBatchRecord? batch)) {
            lock(batch) {
                batch.DispatchedCount = dispatchedCount;
                batch.Status = status;
                batch.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryClaimBatchLeaseAsync(WebhookBatchId batchId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(instanceId);

        if(!this._batches.TryGetValue(batchId, out WebhookPublishBatchRecord? batch)) {
            return ValueTask.FromResult(false);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock(batch) {
            if(batch.LockedBy is not null && batch.LockExpiresAt.HasValue && batch.LockExpiresAt.Value > now && batch.LockedBy != instanceId) {
                return ValueTask.FromResult(false);
            }

            batch.LockedBy = instanceId;
            batch.LockExpiresAt = now.Add(duration);
            batch.Status = WebhookBatchStatus.InFlight;
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<IReadOnlyList<WebhookPublishBatchRecord>> GetStaleInFlightBatchesAsync(DateTimeOffset threshold, int maxCount, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);

        List<WebhookPublishBatchRecord> stale = [];
        foreach(KeyValuePair<WebhookBatchId, WebhookPublishBatchRecord> kvp in this._batches) {
            WebhookPublishBatchRecord batch = kvp.Value;
            if(batch.Status == WebhookBatchStatus.InFlight && batch.LockExpiresAt.HasValue && batch.LockExpiresAt.Value < threshold) {
                stale.Add(batch);
                if(stale.Count >= maxCount) {
                    break;
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<WebhookPublishBatchRecord>>(stale);
    }
}