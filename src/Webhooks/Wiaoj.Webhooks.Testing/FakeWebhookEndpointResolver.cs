namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// Thread-safe test double implementation of <see cref="IWebhookEndpointResolver"/>.
/// </summary>
public sealed class FakeWebhookEndpointResolver : IWebhookEndpointResolver {
    private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new(WebhookEndpointId.OrdinalComparer);
    private readonly Lock _gate = new();
    private int _callCount;

    /// <summary>Gets the total number of endpoint resolution invocations.</summary>
    public int CallCount => this._callCount;

    /// <summary>Initializes a new instance of the <see cref="FakeWebhookEndpointResolver"/> class.</summary>
    public FakeWebhookEndpointResolver() { }

    /// <summary>
    /// Registers an endpoint definition into the resolver.
    /// </summary>
    /// <param name="endpoint">The webhook endpoint to register.</param>
    /// <returns>This instance for method chaining.</returns>
    public FakeWebhookEndpointResolver Register(WebhookEndpoint endpoint) {
        Preca.ThrowIfNull(endpoint);
        lock(this._gate) {
            this._endpoints[endpoint.Id] = endpoint;
        }
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref this._callCount);
        lock(this._gate) {
            this._endpoints.TryGetValue(endpointId, out WebhookEndpoint? endpoint);
            return ValueTask.FromResult(endpoint);
        }
    }

    /// <summary>Clears all registered endpoints.</summary>
    public void Clear() {
        lock(this._gate) {
            this._endpoints.Clear();
        }
    }
}