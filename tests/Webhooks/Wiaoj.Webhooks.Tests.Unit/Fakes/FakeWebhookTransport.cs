using Wiaoj.Preconditions;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class FakeWebhookTransport : IWebhookTransport {
    private readonly List<(WebhookDeliveryJob Job, TimeSpan? Delay)> _enqueuedJobs = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<(WebhookDeliveryJob Job, TimeSpan? Delay)> EnqueuedJobs {
        get {
            lock(this._gate) {
                return [.. this._enqueuedJobs];
            }
        }
    }

    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) =>
        EnqueueAsync(job, null, cancellationToken);

    public Task EnqueueAsync(WebhookDeliveryJob job) =>
        EnqueueAsync(job, null, TestContext.Current.CancellationToken);

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) =>
        EnqueueAsync(job, delay, TestContext.Current.CancellationToken);

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        lock(this._gate) {
            this._enqueuedJobs.Add((job, delay));
        }
        return Task.CompletedTask;
    }

    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(jobs);
        lock(this._gate) {
            for(int i = 0; i < jobs.Count; i++) {
                this._enqueuedJobs.Add((jobs[i], null));
            }
        }
        return Task.CompletedTask;
    }
}