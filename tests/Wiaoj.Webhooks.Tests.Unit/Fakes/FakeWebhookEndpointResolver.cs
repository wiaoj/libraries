namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

internal sealed class FakeWebhookEndpointResolver : IWebhookEndpointResolver {
    private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = [];
    private int _callCount;

    public int CallCount => this._callCount;

    public FakeWebhookEndpointResolver Register(WebhookEndpoint endpoint) {
        this._endpoints[endpoint.Id] = endpoint;
        return this;
    }

    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref _callCount);
        return ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
    }
}