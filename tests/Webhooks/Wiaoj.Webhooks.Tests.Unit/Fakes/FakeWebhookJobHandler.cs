using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class FakeWebhookJobHandler : IWebhookJobHandler {
    private readonly Lock _gate = new();
    private readonly List<WebhookDeliveryJob> _handledJobs = [];

    public bool ThrowOnNextHandle { get; set; }

    public IReadOnlyList<WebhookDeliveryJob> HandledJobs {
        get {
            lock(this._gate) {
                return [.. this._handledJobs];
            }
        }
    }

    public Task<WebhookDeliveryAttempt> HandleAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) {
        if(this.ThrowOnNextHandle) {
            this.ThrowOnNextHandle = false;
            throw new InvalidOperationException("Simulated handler failure.");
        }

        lock(this._gate) {
            this._handledJobs.Add(job);
        }

        return Task.FromResult(WebhookTestFactory.CreateAttempt(job.EndpointId));
    }

    public Task<WebhookDeliveryAttempt> HandleAsync(WebhookDeliveryJob job) =>
        HandleAsync(job, TestContext.Current.CancellationToken);
}