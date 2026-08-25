using Microsoft.Extensions.Logging;
using Wiaoj.Serialization;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// Default implementation of <see cref="IWebhookPublisher"/> orchestrating crash-resilient 1-to-N event fan-out.
/// </summary>
internal sealed class WebhookPublisher : IWebhookPublisher {
    private readonly IWebhookSubscriptionStore _store;
    private readonly IWebhookSubscriptionMatcher _matcher;
    private readonly IWebhookDispatcher _dispatcher;
    private readonly IWebhookEventRegistry _eventRegistry;
    private readonly IWebhookBatchStore _batchStore;
    private readonly ISerializer<WebhookSerializerKey> _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookPublisher> _logger;

    public WebhookPublisher(
        IWebhookSubscriptionStore store,
        IWebhookSubscriptionMatcher matcher,
        IWebhookDispatcher dispatcher,
        IWebhookEventRegistry eventRegistry,
        IWebhookBatchStore batchStore,
        ISerializer<WebhookSerializerKey> serializer,
        TimeProvider timeProvider,
        ILogger<WebhookPublisher> logger) {

        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(matcher);
        Preca.ThrowIfNull(dispatcher);
        Preca.ThrowIfNull(eventRegistry);
        Preca.ThrowIfNull(batchStore);
        Preca.ThrowIfNull(serializer);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._store = store;
        this._matcher = matcher;
        this._dispatcher = dispatcher;
        this._eventRegistry = eventRegistry;
        this._batchStore = batchStore;
        this._serializer = serializer;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        WebhookNamespace @namespace,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        return PublishCoreAsync(@namespace, payload, partitionKey, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        WebhookNamespace @namespace,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        return PublishCoreAsync(@namespace, payload, partitionKey: null, cancellationToken);
    }

    private async Task<IReadOnlyList<WebhookDeliveryHandle>> PublishCoreAsync<TEvent>(
         WebhookNamespace @namespace,
         TEvent payload,
         WebhookPartitionKey? partitionKey,
         CancellationToken cancellationToken)
         where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        string eventName = this._eventRegistry.GetEventName<TEvent>();
        IReadOnlyList<WebhookSubscription> activeSubscriptions = await this._store
            .GetActiveSubscriptionsAsync(@namespace, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        HashSet<WebhookEndpointId> visitedEndpoints = new(WebhookEndpointId.OrdinalComparer);
        List<WebhookSubscription> matchedSubscriptions = [];

        for(int i = 0; i < activeSubscriptions.Count; i++) {
            WebhookSubscription sub = activeSubscriptions[i];
            if(this._matcher.Matches(sub, eventName, payload) && visitedEndpoints.Add(sub.EndpointId)) {
                matchedSubscriptions.Add(sub);
            }
        }

        if(matchedSubscriptions.Count == 0) {
            this._logger.LogDebug("No matching subscriptions found for event '{EventName}' in namespace '{Namespace}'. Skipping fan-out.", eventName, @namespace.Value);
            return [];
        }

        this._logger.LogInformation("Fanning out event '{EventName}' to {SubscriberCount} matching subscribers in namespace '{Namespace}'.", eventName, matchedSubscriptions.Count, @namespace.Value);

        WebhookBatchId batchId = WebhookBatchId.NewId();
        string serializedPayload = this._serializer.SerializeToString(payload, payload.GetType());
        List<WebhookEndpointId> targetEndpoints = matchedSubscriptions.Select(s => s.EndpointId).ToList();

        WebhookPublishBatchRecord batchRecord = new(
            batchId,
            @namespace,
            eventName,
            serializedPayload,
            targetEndpoints,
            this._timeProvider.GetUtcNow());

        await this._batchStore.SaveBatchAsync(batchRecord, cancellationToken).ConfigureAwait(false);

        List<WebhookDeliveryHandle> handles = new(matchedSubscriptions.Count);

        try {
            for(int i = 0; i < matchedSubscriptions.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                WebhookSubscription sub = matchedSubscriptions[i];
                WebhookPartitionKey effectivePartitionKey = partitionKey ?? WebhookPartitionKey.From(sub.EndpointId);

                WebhookDeliveryHandle handle = await this._dispatcher.DispatchAsync(
                    sub.EndpointId,
                    payload,
                    effectivePartitionKey,
                    cancellationToken).ConfigureAwait(false);

                handles.Add(handle);
            }

            await this._batchStore.UpdateBatchProgressAsync(
                batchId,
                handles.Count,
                WebhookBatchStatus.Completed,
                CancellationToken.None).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            return handles;
        }
        catch(OperationCanceledException) {
            WebhookBatchStatus status = handles.Count > 0
                ? WebhookBatchStatus.PartiallyCompleted
                : WebhookBatchStatus.Pending;

            await this._batchStore.UpdateBatchProgressAsync(
                batchId,
                handles.Count,
                status,
                CancellationToken.None).ConfigureAwait(false);

            throw;
        }
        catch(Exception) {
            WebhookBatchStatus status = handles.Count > 0
                ? WebhookBatchStatus.PartiallyCompleted
                : WebhookBatchStatus.Failed;

            await this._batchStore.UpdateBatchProgressAsync(
                batchId,
                handles.Count,
                status,
                CancellationToken.None).ConfigureAwait(false);

            throw;
        }
    }
}