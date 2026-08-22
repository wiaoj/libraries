using System.Collections.Concurrent;
using Wiaoj.Security;
using Wiaoj.Webhooks;

namespace Wiaoj.Samples.Webhooks.Infrastructure;

public sealed class SampleEndpointStore : IWebhookEndpointResolver {
    private readonly ConcurrentDictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new();

    public void Register(WebhookEndpoint endpoint) {
        this._endpoints[endpoint.Id] = endpoint;
    }

    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        this._endpoints.TryGetValue(endpointId, out WebhookEndpoint? endpoint);
        return ValueTask.FromResult(endpoint);
    }
}
