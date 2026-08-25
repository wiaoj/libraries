using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

internal sealed class WebhookDispatcher : IWebhookDispatcher {
    private readonly IWebhookStore _store;
    private readonly IWebhookTransport _transport;
    private readonly IWebhookEndpointResolver _endpointResolver;
    private readonly WebhookPipelineRunner _pipelineRunner;
    private readonly ISerializer<WebhookSerializerKey> _serializer;
    private readonly IWebhookEventRegistry _eventRegistry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        IWebhookStore store,
        IWebhookTransport transport,
        IWebhookEndpointResolver endpointResolver,
        WebhookPipelineRunner pipelineRunner,
        ISerializer<WebhookSerializerKey> serializer,
        IWebhookEventRegistry eventRegistry,
        TimeProvider timeProvider,
        ILogger<WebhookDispatcher> logger) {

        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(transport);
        Preca.ThrowIfNull(endpointResolver);
        Preca.ThrowIfNull(pipelineRunner);
        Preca.ThrowIfNull(serializer);
        Preca.ThrowIfNull(eventRegistry);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._store = store;
        this._transport = transport;
        this._endpointResolver = endpointResolver;
        this._pipelineRunner = pipelineRunner;
        this._serializer = serializer;
        this._eventRegistry = eventRegistry;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(payload);
        Preca.ThrowIfNullOrWhiteSpace(partitionKey.Value);

        cancellationToken.ThrowIfCancellationRequested();

        string eventName = this._eventRegistry.GetEventName<TEvent>();
        this._logger.LogDispatchStarting(eventName, endpointId);

        using Activity? activity = WebhookActivitySource.StartDispatchActivity(endpointId, eventName);

        try {
            WebhookJobId jobId = WebhookJobId.NewJobId();
            string serializedPayload = this._serializer.SerializeToString(payload, payload.GetType());

            cancellationToken.ThrowIfCancellationRequested();

            WebhookJobRecord jobRecord = new(
                jobId,
                endpointId,
                partitionKey.Value,
                eventName,
                serializedPayload,
                this._timeProvider.GetUtcNow());

            await this._store.SaveAsync(jobRecord, cancellationToken).ConfigureAwait(false);

            WebhookDeliveryJob job = new(jobId, endpointId, partitionKey, eventName, payload);
            await this._transport.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);

            WebhookMeter.DispatchedEventsCount.Add(1, new TagList {
                { "webhook.endpoint_id", endpointId.Value },
                { "webhook.partition_key", partitionKey.Value },
                { "webhook.event_name", eventName }
            });

            this._logger.LogDispatchCompleted(eventName, jobId, endpointId);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new WebhookDeliveryHandle(jobId);
        }
        catch(Exception ex) {
            WebhookMeter.DispatchErrorCount.Add(1, new TagList {
                { "webhook.endpoint_id", endpointId.Value },
                { "webhook.partition_key", partitionKey.Value },
                { "webhook.event_name", eventName }
            });

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            this._logger.LogDispatchFailed(ex, eventName, endpointId);
            throw;
        }
    }
     
    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookDeliveryHandle>> DispatchBatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        IEnumerable<TEvent> payloads,
        Func<TEvent, WebhookPartitionKey>? partitionKeySelector,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {

        Preca.ThrowIfNull(payloads);

        cancellationToken.ThrowIfCancellationRequested();

        TEvent[] eventArray = payloads as TEvent[] ?? [.. payloads];
        if(eventArray.Length == 0) {
            return [];
        }

        string eventName = this._eventRegistry.GetEventName<TEvent>();
        string batchId = $"batch_{Guid.CreateVersion7():N}";
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        this._logger.LogBatchDispatchStarting(batchId, eventArray.Length, endpointId);
        using Activity? activity = WebhookActivitySource.StartBatchDispatchActivity(endpointId, batchId, eventArray.Length);

        WebhookJobRecord[] records = new WebhookJobRecord[eventArray.Length];
        WebhookDeliveryJob[] deliveryJobs = new WebhookDeliveryJob[eventArray.Length];
        WebhookDeliveryHandle[] handles = new WebhookDeliveryHandle[eventArray.Length];

        try {
            for(int i = 0; i < eventArray.Length; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                TEvent payload = eventArray[i];
                Preca.ThrowIfNull(payload);

                WebhookJobId jobId = WebhookJobId.NewJobId();
                WebhookPartitionKey partitionKey = partitionKeySelector?.Invoke(payload) ?? WebhookPartitionKey.From(endpointId);
                string serialized = this._serializer.SerializeToString(payload, payload.GetType());

                records[i] = new WebhookJobRecord(jobId, endpointId, partitionKey.Value, eventName, serialized, now) {
                    BatchId = batchId
                };

                deliveryJobs[i] = new WebhookDeliveryJob(jobId, endpointId, partitionKey, eventName, payload);
                handles[i] = new WebhookDeliveryHandle(jobId);
            }

            // 1. Single batch database persistence
            await this._store.SaveBatchAsync(records, cancellationToken).ConfigureAwait(false);

            // 2. Single batch transport enqueue
            await this._transport.EnqueueBatchAsync(deliveryJobs, cancellationToken).ConfigureAwait(false);

            TagList batchTags = new() {
                { "webhook.endpoint_id", endpointId.Value },
                { "webhook.event_name", eventName }
            };

            WebhookMeter.DispatchedEventsCount.Add(eventArray.Length, batchTags);
            WebhookMeter.BatchDispatchCount.Add(1, batchTags);
            WebhookMeter.BatchSizeHistogram.Record(eventArray.Length, batchTags);

            this._logger.LogBatchDispatchCompleted(batchId, eventArray.Length, endpointId);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return handles;
        }
        catch(Exception ex) {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            this._logger.LogBatchDispatchFailed(ex, batchId, eventArray.Length, endpointId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        WebhookJobRecord? jobRecord = await this._store.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot replay non-existent job '{jobId}'.");

        cancellationToken.ThrowIfCancellationRequested();

        if(!this._eventRegistry.TryGetEventType(jobRecord.EventType, out Type? eventType) || eventType is null) {
            throw new InvalidOperationException($"Cannot replay job '{jobId}': Event type '{jobRecord.EventType}' is not registered in the event registry.");
        }

        object? deserialized = this._serializer.DeserializeFromString(jobRecord.SerializedPayload, eventType);
        if(deserialized is not IWebhookEvent domainEvent) {
            throw new InvalidOperationException($"Cannot replay job '{jobId}': Deserialized payload does not implement IWebhookEvent.");
        }

        jobRecord.Status = WebhookJobStatus.Queued;
        await this._store.UpdateStatusAsync(jobId, WebhookJobStatus.Queued, cancellationToken).ConfigureAwait(false);

        WebhookDeliveryJob job = new(jobId,
                                     jobRecord.EndpointId,
                                     WebhookPartitionKey.Parse(jobRecord.PartitionKey),
                                     jobRecord.EventType,
                                     domainEvent) {
            IsReplay = true
        };

        await this._transport.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);

        return new WebhookDeliveryHandle(jobId);
    }

    /// <inheritdoc/>
    public async Task<WebhookPingResult> PingAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        WebhookEndpoint? endpoint = await this._endpointResolver.ResolveAsync(endpointId, cancellationToken).ConfigureAwait(false);
        if(endpoint is null) {
            return new WebhookPingResult(false, null, TimeSpan.Zero, null, null, $"Endpoint '{endpointId.Value}' not found.", string.Empty);
        }

        string pingId = $"ping_{Guid.CreateVersion7():N}";
        WebhookPingEvent pingPayload = new(pingId, this._timeProvider.GetUtcNow());
        string serialized = this._serializer.SerializeToString<WebhookPingEvent>(pingPayload);

        WebhookDeliveryContext context = new() {
            JobId = WebhookJobId.NewJobId(),
            Endpoint = endpoint,
            PartitionKey = WebhookPartitionKey.From(endpointId),
            EventType = "webhook.ping",
            Event = pingPayload,
            SerializedPayload = serialized,
            AttemptHistory = []
        };

        Stopwatch stopwatch = Stopwatch.StartNew();
        try {
            WebhookDeliveryAttempt attempt = await this._pipelineRunner.RunAsync(context, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            int? statusCode = attempt.Result switch {
                WebhookDeliveryResult.Delivered d => d.StatusCode,
                WebhookDeliveryResult.TransientFailure tf => tf.StatusCode,
                WebhookDeliveryResult.PermanentFailure pf => pf.StatusCode,
                _ => null
            };

            string? responseBody = attempt.Result switch {
                WebhookDeliveryResult.Delivered d => d.ResponseBody,
                _ => null
            };

            string? error = attempt.Result switch {
                WebhookDeliveryResult.TransientFailure tf => tf.ErrorMessage,
                WebhookDeliveryResult.PermanentFailure pf => pf.ErrorMessage,
                _ => null
            };

            return new WebhookPingResult(
                attempt.IsSuccess,
                statusCode,
                stopwatch.Elapsed,
                endpoint.TargetUrl.Host,
                responseBody,
                error,
                pingId);
        }
        catch(Exception ex) {
            stopwatch.Stop();
            return new WebhookPingResult(false, null, stopwatch.Elapsed, endpoint.TargetUrl.Host, null, ex.Message, pingId);
        }
    }
}