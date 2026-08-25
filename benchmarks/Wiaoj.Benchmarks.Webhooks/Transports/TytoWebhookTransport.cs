using Tyto;
using Wiaoj.Preconditions;
using Wiaoj.Serialization;
using Wiaoj.Webhooks;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

/// <summary>
/// Tyto event envelope holding wire-format webhook job data without polymorphic interface properties.
/// </summary>
[Message("webhook.delivery.job", 1)]
public sealed record TytoWebhookJobEnvelope(
    string JobId,
    string EndpointId,
    string PartitionKey,
    string EventType,
    string SerializedPayload) : IEvent;

/// <summary>
/// Tyto consumer rehydrating the domain payload and executing the webhook delivery job handler.
/// </summary>
public sealed class TytoWebhookJobHandler : IEventHandler<TytoWebhookJobEnvelope> {
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebhookEventRegistry _eventRegistry;
    private readonly IWebhookJobHandler _jobHandler;
    private readonly ISerializer<WebhookSerializerKey> _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TytoWebhookJobHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider instance.</param>
    /// <param name="eventRegistry">The webhook event registry.</param>
    /// <param name="serializer">The webhook serializer.</param>
    public TytoWebhookJobHandler(
        IServiceProvider serviceProvider,
        IWebhookEventRegistry eventRegistry,
        IWebhookJobHandler jobHandler,
        ISerializer<WebhookSerializerKey> serializer) {
        Preca.ThrowIfNull(serviceProvider);
        Preca.ThrowIfNull(eventRegistry);
        Preca.ThrowIfNull(serializer);

        this._serviceProvider = serviceProvider;
        this._eventRegistry = eventRegistry;
        this._jobHandler = jobHandler;
        this._serializer = serializer;
    }

    /// <inheritdoc/>
    public async ValueTask HandleAsync(IMessageContext<TytoWebhookJobEnvelope> context, CancellationToken cancellationToken = default) {
        TytoWebhookJobEnvelope envelope = context.Message;

        if(!this._eventRegistry.TryGetEventType(envelope.EventType, out Type? eventType) || eventType is null) {
            Console.WriteLine("TryGetEventType");
            Console.WriteLine(envelope.EventType);
            return;
        }

        object? payloadObj = this._serializer.DeserializeFromString(envelope.SerializedPayload, eventType);
        if(payloadObj is not IWebhookEvent domainEvent) {
            Console.WriteLine("DeserializeFromString");
            return;
        }

        WebhookDeliveryJob job = new(
            WebhookJobId.Parse(envelope.JobId),
            WebhookEndpointId.Parse(envelope.EndpointId),
            WebhookPartitionKey.Parse(envelope.PartitionKey),
            envelope.EventType,
            domainEvent);

        await this._jobHandler.HandleAsync(job, cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref ProcessedCounters.Tyto);
    }
}

/// <summary>
/// Webhook transport implementation backed by Tyto message bus using wire-format envelopes.
/// </summary>
public sealed class TytoWebhookTransport : IWebhookTransport {
    private readonly IBus _bus;
    private readonly ISerializer<WebhookSerializerKey> _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TytoWebhookTransport"/> class.
    /// </summary>
    /// <param name="bus">The Tyto bus instance.</param>
    /// <param name="serializer">The webhook serializer.</param>
    public TytoWebhookTransport(IBus bus, ISerializer<WebhookSerializerKey> serializer) {
        Preca.ThrowIfNull(bus);
        Preca.ThrowIfNull(serializer);

        this._bus = bus;
        this._serializer = serializer;
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);

        Interlocked.Increment(ref ProcessedCounters.SentTyto);

        string serializedPayload = this._serializer.SerializeToString(job.Payload, job.Payload.GetType());

        TytoWebhookJobEnvelope envelope = new(
            job.Id.Value,
            job.EndpointId.Value,
            job.PartitionKey.Value,
            job.EventType,
            serializedPayload);

        return this._bus.PublishAsync(envelope, cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) {
        return EnqueueAsync(job, null, CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) {
        return EnqueueAsync(job, delay, CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        return EnqueueAsync(job, cancellationToken);
    }
}