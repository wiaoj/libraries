namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// Test double of <see cref="IWebhookPublisher"/> recording all published domain events.
/// </summary>
public sealed class FakeWebhookPublisher : IWebhookPublisher {
    private readonly List<PublishedCall> _calls = [];
    private readonly Lock _gate = new();

    /// <summary>Gets the chronological list of all recorded published calls.</summary>
    public IReadOnlyList<PublishedCall> Calls {
        get {
            lock(this._gate) {
                return [.. this._calls];
            }
        }
    }

    /// <summary>Initializes a new instance of the <see cref="FakeWebhookPublisher"/> class.</summary>
    public FakeWebhookPublisher() { }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        WebhookNamespace @namespace,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(payload);

        WebhookJobId jobId = WebhookJobId.NewJobId();
        PublishedCall call = new(@namespace, payload, partitionKey, typeof(TEvent));

        lock(this._gate) {
            this._calls.Add(call);
        }

        IReadOnlyList<WebhookDeliveryHandle> handles = [new WebhookDeliveryHandle(jobId)];
        return Task.FromResult(handles);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        WebhookNamespace @namespace,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        return PublishAsync(@namespace, payload, WebhookPartitionKey.Parse(@namespace.Value), cancellationToken);
    }

    /// <summary>Clears all recorded publish calls.</summary>
    public void Clear() {
        lock(this._gate) {
            this._calls.Clear();
        }
    }

    /// <summary>Represents a recorded publish invocation details.</summary>
    public sealed record PublishedCall(
        WebhookNamespace Namespace,
        object Payload,
        WebhookPartitionKey PartitionKey,
        Type EventType);
}