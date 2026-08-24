namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;

internal sealed class FakeWebhookDispatcher : IWebhookDispatcher {
    private readonly List<DispatchedCall> _calls = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Optional callback invoked immediately after a dispatch call is recorded.
    /// Useful for simulating mid-flight cancellations or fault injection in tests.
    /// </summary>
    public Action<DispatchedCall>? OnDispatched { get; set; }

    public IReadOnlyList<DispatchedCall> Calls {
        get {
            lock(this._gate) {
                return [.. this._calls];
            }
        }
    }

    public Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {

        WebhookJobId jobId = WebhookJobId.NewJobId();
        DispatchedCall call = new(endpointId, payload!, partitionKey, jobId);

        lock(this._gate) {
            this._calls.Add(call);
        }

        this.OnDispatched?.Invoke(call);

        return Task.FromResult(new WebhookDeliveryHandle(jobId));
    }

    public Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        return Task.FromResult(new WebhookDeliveryHandle(jobId));
    }

    public sealed record DispatchedCall(
        WebhookEndpointId EndpointId,
        object Payload,
        WebhookPartitionKey PartitionKey,
        WebhookJobId JobId);
}