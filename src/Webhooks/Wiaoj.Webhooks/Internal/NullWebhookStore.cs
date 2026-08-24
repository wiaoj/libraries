namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// No-op implementation of <see cref="IWebhookStore"/> used when persistent job auditing is disabled.
/// </summary>
public sealed class NullWebhookStore : IWebhookStore {
    /// <summary>
    /// Gets the singleton instance of <see cref="NullWebhookStore"/>.
    /// </summary>
    public static NullWebhookStore Instance { get; } = new();

    /// <inheritdoc/>
    public Task SaveAsync(WebhookJobRecord job, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<WebhookJobRecord?> GetJobAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        return Task.FromResult<WebhookJobRecord?>(null);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetHistoryByEndpointAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>([]);
    }

    /// <inheritdoc/>
    public Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TryClaimLeaseAsync(WebhookJobId jobId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default) {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task RecordAttemptAsync(WebhookJobId jobId, WebhookDeliveryAttempt attempt, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetStaleJobsAsync(DateTimeOffset? inFlightThreshold,
                                                                   DateTimeOffset? queuedThreshold,
                                                                   int maxCount,
                                                                   CancellationToken cancellationToken = default) {
        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>([]);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(int maxCount, CancellationToken cancellationToken = default) {
        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>([]);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(WebhookEndpointId endpointId, int maxCount, CancellationToken cancellationToken = default) {
        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>([]);
    }
}