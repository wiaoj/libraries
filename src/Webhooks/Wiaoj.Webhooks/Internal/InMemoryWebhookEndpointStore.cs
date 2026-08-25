using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Thread-safe in-memory directory and resolver for webhook endpoints.
/// </summary>
internal sealed class InMemoryWebhookEndpointStore : IWebhookEndpointResolver {
    private readonly ConcurrentDictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new(WebhookEndpointId.OrdinalComparer);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookEndpointStore"/> class.
    /// </summary>
    public InMemoryWebhookEndpointStore() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookEndpointStore"/> class with predefined endpoints.
    /// </summary>
    /// <param name="endpoints">The collection of endpoints to preload.</param>
    public InMemoryWebhookEndpointStore(IEnumerable<WebhookEndpoint> endpoints) {
        Preca.ThrowIfNull(endpoints);

        foreach(WebhookEndpoint endpoint in endpoints) {
            this._endpoints[endpoint.Id] = endpoint;
        }
    }

    /// <summary>
    /// Registers or updates an endpoint in memory.
    /// </summary>
    /// <param name="endpoint">The endpoint instance to register.</param>
    public void Register(WebhookEndpoint endpoint) {
        Preca.ThrowIfNull(endpoint);
        this._endpoints[endpoint.Id] = endpoint;
    }

    /// <inheritdoc/>
    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(endpointId.Value);
        this._endpoints.TryGetValue(endpointId, out WebhookEndpoint? endpoint);
        return ValueTask.FromResult(endpoint);
    }
}