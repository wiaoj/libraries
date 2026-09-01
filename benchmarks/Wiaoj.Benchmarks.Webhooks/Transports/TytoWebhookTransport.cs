using Tyto;
using Wiaoj.Preconditions;
using Wiaoj.Serialization;
using Wiaoj.Webhooks;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

[Message("webhook.delivery.job", 1)]
public sealed record TytoWebhookJobEnvelope(
    string JobId,
    string EndpointId,
    string PartitionKey,
    string EventType,
    string SerializedPayload) : IEvent;

public sealed class TytoWebhookJobHandler : IEventHandler<TytoWebhookJobEnvelope> {
    private readonly IWebhookEventRegistry _eventRegistry;
    private readonly IWebhookJobHandler _jobHandler;
    private readonly ISerializer<WebhookSerializerKey> _serializer;

    public TytoWebhookJobHandler(
        IWebhookEventRegistry eventRegistry,
        IWebhookJobHandler jobHandler,
        ISerializer<WebhookSerializerKey> serializer) {
        this._eventRegistry = eventRegistry;
        this._jobHandler = jobHandler;
        this._serializer = serializer;
    }

    public async ValueTask HandleAsync(IMessageContext<TytoWebhookJobEnvelope> context, CancellationToken cancellationToken = default) {
        TytoWebhookJobEnvelope envelope = context.Message;

        if(!this._eventRegistry.TryGetEventType(envelope.EventType, out Type? eventType) || eventType is null) {
            return;
        }

        object? payloadObj = this._serializer.DeserializeFromString(envelope.SerializedPayload, eventType);
        if(payloadObj is not IWebhookEvent domainEvent) {
            return;
        }

        WebhookDeliveryJob job = new(
            WebhookJobId.Parse(envelope.JobId),
            WebhookEndpointId.Parse(envelope.EndpointId),
            WebhookPartitionKey.Parse(envelope.PartitionKey),
            envelope.EventType,
            domainEvent);

        await this._jobHandler.HandleAsync(job, cancellationToken).ConfigureAwait(false);

        BenchmarkCompletionTracker.SignalItemCompleted();
    }
}

public sealed class TytoWebhookTransport(IBus bus, ISerializer<WebhookSerializerKey> serializer) : IWebhookTransport {
    private readonly IBus _bus = bus;
    private readonly ISerializer<WebhookSerializerKey> _serializer = serializer;

    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);

        string serializedPayload = this._serializer.SerializeToString(job.Payload, job.Payload.GetType());

        TytoWebhookJobEnvelope envelope = new(
            job.Id.Value,
            job.EndpointId.Value,
            job.PartitionKey.Value,
            job.EventType,
            serializedPayload);

        return this._bus.PublishAsync(envelope, cancellationToken).AsTask();
    }

    public Task EnqueueAsync(WebhookDeliveryJob job) => EnqueueAsync(job, CancellationToken.None);
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => EnqueueAsync(job, CancellationToken.None);
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) => EnqueueAsync(job, cancellationToken);

    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}