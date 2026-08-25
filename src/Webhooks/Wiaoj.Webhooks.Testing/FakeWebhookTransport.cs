namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// In-memory test double of <see cref="IWebhookTransport"/> recording all enqueued units of work.
/// </summary>
public sealed class FakeWebhookTransport : IWebhookTransport {
    private readonly List<(WebhookDeliveryJob Job, TimeSpan? Delay)> _enqueuedJobs = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Gets all delivery jobs recorded by the transport.
    /// </summary>
    public IReadOnlyList<(WebhookDeliveryJob Job, TimeSpan? Delay)> EnqueuedJobs {
        get {
            lock(this._gate) {
                return [.. this._enqueuedJobs];
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookTransport"/> class.
    /// </summary>
    public FakeWebhookTransport() { }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) =>
        EnqueueAsync(job, null, cancellationToken);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) =>
        EnqueueAsync(job, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) =>
        EnqueueAsync(job, delay, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);
        lock(this._gate) {
            this._enqueuedJobs.Add((job, delay));
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(jobs);
        lock(this._gate) {
            for(int i = 0; i < jobs.Count; i++) {
                this._enqueuedJobs.Add((jobs[i], null));
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all recorded enqueued jobs.
    /// </summary>
    public void Clear() {
        lock(this._gate) {
            this._enqueuedJobs.Clear();
        }
    }
}