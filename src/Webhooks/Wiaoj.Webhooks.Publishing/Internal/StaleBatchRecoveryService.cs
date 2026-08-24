using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Serialization;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// Background worker periodically sweeping and recovering incomplete or crashed 1-to-N fan-out batches.
/// </summary>
internal sealed class StaleBatchRecoveryService : BackgroundService {
    private readonly IWebhookBatchStore _batchStore;
    private readonly IWebhookDispatcher _dispatcher;
    private readonly IWebhookEventRegistry _eventRegistry;
    private readonly ISerializer<WebhookSerializerKey> _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaleBatchRecoveryService> _logger;
    private readonly string _instanceId;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(2);

    public StaleBatchRecoveryService(
        IWebhookBatchStore batchStore,
        IWebhookDispatcher dispatcher,
        IWebhookEventRegistry eventRegistry,
        ISerializer<WebhookSerializerKey> serializer,
        IOptions<WebhookOptions> webhookOptions,
        TimeProvider timeProvider,
        ILogger<StaleBatchRecoveryService> logger) {

        Preca.ThrowIfNull(batchStore);
        Preca.ThrowIfNull(dispatcher);
        Preca.ThrowIfNull(eventRegistry);
        Preca.ThrowIfNull(serializer);
        Preca.ThrowIfNull(webhookOptions);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._batchStore = batchStore;
        this._dispatcher = dispatcher;
        this._eventRegistry = eventRegistry;
        this._serializer = serializer;
        this._instanceId = webhookOptions.Value.InstanceId;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using PeriodicTimer timer = new(this._pollingInterval, this._timeProvider);

        while(!stoppingToken.IsCancellationRequested) {
            try {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                await SweepAndRecoverAsync(stoppingToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                break;
            }
            catch(Exception ex) {
                this._logger.LogError(ex, "Unexpected error during stale batch recovery sweep.");
            }
        }
    }

    public async Task<int> SweepAndRecoverAsync(CancellationToken cancellationToken = default) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        IReadOnlyList<WebhookPublishBatchRecord> staleBatches = await this._batchStore.GetStaleInFlightBatchesAsync(now, 50, cancellationToken).ConfigureAwait(false);

        if(staleBatches.Count == 0) {
            return 0;
        }

        int recoveredBatchCount = 0;

        foreach(WebhookPublishBatchRecord batch in staleBatches) {
            if(cancellationToken.IsCancellationRequested) break;

            bool leaseClaimed = await this._batchStore.TryClaimBatchLeaseAsync(batch.Id, this._instanceId, this._leaseDuration, cancellationToken).ConfigureAwait(false);
            if(!leaseClaimed) continue;

            try {
                // 1. Resolve actual CLR type using the Event Registry
                if(!this._eventRegistry.TryGetEventType(batch.EventName, out Type? eventType) || eventType is null) {
                    this._logger.LogError("Cannot recover batch '{BatchId}': Event type '{EventName}' is not registered in the event registry.", batch.Id.Value, batch.EventName);
                    await this._batchStore.UpdateBatchProgressAsync(batch.Id, batch.DispatchedCount, WebhookBatchStatus.Failed, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // 2. Deserialize payload back into strongly-typed domain event instance
                object? deserialized = this._serializer.DeserializeFromString(batch.SerializedPayload, eventType);
                if(deserialized is not IWebhookEvent domainEvent) {
                    this._logger.LogError("Cannot recover batch '{BatchId}': Deserialized object does not implement IWebhookEvent.", batch.Id.Value);
                    await this._batchStore.UpdateBatchProgressAsync(batch.Id, batch.DispatchedCount, WebhookBatchStatus.Failed, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // 3. Resume dispatching strictly for unreached target subscribers
                for(int i = batch.DispatchedCount; i < batch.TargetEndpoints.Count; i++) {
                    WebhookEndpointId endpointId = batch.TargetEndpoints[i];
                    WebhookPartitionKey partitionKey = WebhookPartitionKey.From(endpointId);

                    await this._dispatcher.DispatchAsync(endpointId, domainEvent, partitionKey, cancellationToken).ConfigureAwait(false);
                }

                await this._batchStore.UpdateBatchProgressAsync(batch.Id, batch.TargetEndpoints.Count, WebhookBatchStatus.Completed, cancellationToken).ConfigureAwait(false);
                recoveredBatchCount++;
            }
            catch(Exception ex) {
                this._logger.LogError(ex, "Failed to recover publish batch '{BatchId}'.", batch.Id.Value);
            }
        }

        return recoveredBatchCount;
    }
}