namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// Thread-safe test double implementation of <see cref="IWebhookDispatcher"/>
/// that records all dispatched events and allows verifying dispatch assertions.
/// </summary>
public sealed class FakeWebhookDispatcher : IWebhookDispatcher {
    private readonly List<DispatchedCall> _calls = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Gets or sets an optional callback invoked immediately after a dispatch call is recorded.
    /// Useful for fault injection or simulating cancellations in tests.
    /// </summary>
    public Action<DispatchedCall>? OnDispatched { get; set; }

    /// <summary>
    /// Gets the chronological list of all recorded dispatch calls.
    /// </summary>
    public IReadOnlyList<DispatchedCall> Calls {
        get {
            lock(this._gate) {
                return [.. this._calls];
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebhookDispatcher"/> class.
    /// </summary>
    public FakeWebhookDispatcher() { }

    /// <inheritdoc/>
    public Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(payload);
        Preca.ThrowIfNullOrWhiteSpace(partitionKey.Value);

        WebhookJobId jobId = WebhookJobId.NewJobId();
        DispatchedCall call = new(endpointId, payload, partitionKey, typeof(TEvent), jobId);

        lock(this._gate) {
            this._calls.Add(call);
        }

        this.OnDispatched?.Invoke(call);

        return Task.FromResult(new WebhookDeliveryHandle(jobId));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookDeliveryHandle>> DispatchBatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        IEnumerable<TEvent> payloads,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        return DispatchBatchAsync(endpointId, payloads, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookDeliveryHandle>> DispatchBatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        IEnumerable<TEvent> payloads,
        Func<TEvent, WebhookPartitionKey>? partitionKeySelector,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(payloads);
        List<WebhookDeliveryHandle> handles = [];

        foreach(TEvent payload in payloads) {
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookPartitionKey partitionKey = partitionKeySelector?.Invoke(payload) ?? WebhookPartitionKey.From(endpointId);
            DispatchedCall call = new(endpointId, payload, partitionKey, typeof(TEvent), jobId);

            lock(this._gate) {
                this._calls.Add(call);
            }

            this.OnDispatched?.Invoke(call);
            handles.Add(new WebhookDeliveryHandle(jobId));
        }

        return Task.FromResult<IReadOnlyList<WebhookDeliveryHandle>>(handles);
    }

    /// <inheritdoc/>
    public Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        return Task.FromResult(new WebhookDeliveryHandle(jobId));
    }

    /// <inheritdoc/>
    public Task<WebhookPingResult> PingAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        return Task.FromResult(new WebhookPingResult(
            isSuccess: true,
            statusCode: 200,
            latency: TimeSpan.FromMilliseconds(20),
            errorMessage: null));
    }

    /// <summary>
    /// Determines whether any event of type <typeparamref name="TEvent"/> was dispatched.
    /// </summary>
    /// <typeparam name="TEvent">The expected event payload type.</typeparam>
    /// <returns><see langword="true"/> if matching events were dispatched; otherwise, <see langword="false"/>.</returns>
    public bool HasDispatched<TEvent>() where TEvent : IWebhookEvent {
        lock(this._gate) {
            return this._calls.Any(c => c.EventType == typeof(TEvent));
        }
    }

    /// <summary>
    /// Retrieves all dispatched event payloads of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event payload type.</typeparam>
    /// <returns>A collection of matching typed event instances.</returns>
    public IReadOnlyList<TEvent> GetDispatchedPayloads<TEvent>() where TEvent : IWebhookEvent {
        lock(this._gate) {
            return this._calls
                .Where(c => c.EventType == typeof(TEvent))
                .Select(c => (TEvent)c.Payload)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all recorded dispatch calls.
    /// </summary>
    public void Clear() {
        lock(this._gate) {
            this._calls.Clear();
        }
    }

    /// <summary>
    /// Represents a recorded dispatch invocation details.
    /// </summary>
    public sealed record DispatchedCall(
        WebhookEndpointId EndpointId,
        object Payload,
        WebhookPartitionKey PartitionKey,
        Type EventType,
        WebhookJobId JobId);
}