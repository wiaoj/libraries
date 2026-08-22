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
        EnqueueAsync(job, null, CancellationToken.None);

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) =>
        EnqueueAsync(job, delay, CancellationToken.None);

    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        lock(this._gate) {
            this._enqueuedJobs.Add((job, delay));
        }
        return Task.CompletedTask;
    }
}
