namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// No-op implementation of <see cref="IWebhookBatchStore"/> used when persistent batch auditing is disabled.
/// </summary>
internal sealed class NullWebhookBatchStore : IWebhookBatchStore {
    public static NullWebhookBatchStore Instance { get; } = new();

    public ValueTask SaveBatchAsync(WebhookPublishBatchRecord batch, CancellationToken cancellationToken = default) {
        return ValueTask.CompletedTask;
    }

    public ValueTask<WebhookPublishBatchRecord?> GetBatchAsync(WebhookBatchId batchId, CancellationToken cancellationToken = default) {
        return ValueTask.FromResult<WebhookPublishBatchRecord?>(null);
    }

    public ValueTask UpdateBatchProgressAsync(WebhookBatchId batchId, int dispatchedCount, WebhookBatchStatus status, CancellationToken cancellationToken = default) {
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryClaimBatchLeaseAsync(WebhookBatchId batchId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default) {
        return ValueTask.FromResult(true);
    }

    public ValueTask<IReadOnlyList<WebhookPublishBatchRecord>> GetStaleInFlightBatchesAsync(DateTimeOffset threshold, int maxCount, CancellationToken cancellationToken = default) {
        return ValueTask.FromResult<IReadOnlyList<WebhookPublishBatchRecord>>([]);
    }
}