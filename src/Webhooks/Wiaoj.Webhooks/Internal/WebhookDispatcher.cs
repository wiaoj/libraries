using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

internal sealed class WebhookDispatcher : IWebhookDispatcher {
    private readonly IWebhookStore _store;
    private readonly IWebhookTransport _transport;
    private readonly ISerializer<WebhookSerializerKey> _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        IWebhookStore store,
        IWebhookTransport transport,
        ISerializer<WebhookSerializerKey> serializer,
        TimeProvider timeProvider,
        ILogger<WebhookDispatcher> logger) {
        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(transport);
        Preca.ThrowIfNull(serializer);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._store = store;
        this._transport = transport;
        this._serializer = serializer;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(payload);

        string eventName = !string.IsNullOrWhiteSpace(TEvent.EventName)
            ? TEvent.EventName
            : typeof(TEvent).Name;

        this._logger.LogDispatchStarting(eventName, endpointId);

        using Activity? activity = WebhookActivitySource.StartDispatchActivity(endpointId, eventName);

        try {
            WebhookJobId jobId = WebhookJobId.NewJobId();
            string serializedPayload = this._serializer.SerializeToString(payload, payload.GetType());

            // 1. Store-First Persistence with deterministic TimeProvider (Zero loss guarantee)
            this._logger.LogStoreSavingJob(jobId, endpointId, eventName);
            WebhookJobRecord jobRecord = new(
                jobId,
                endpointId,
                eventName,
                serializedPayload,
                this._timeProvider.GetUtcNow());

            await this._store.SaveAsync(jobRecord, cancellationToken).ConfigureAwait(false);

            // 2. Zero-Latency Execution Channel Push (Fast-Path)
            WebhookDeliveryJob job = new(jobId, endpointId, payload);
            await this._transport.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);

            WebhookMeter.DispatchedEventsCount.Add(1, new TagList {
                { "webhook.endpoint_id", endpointId.Value },
                { "webhook.event_name", eventName }
            });

            this._logger.LogDispatchCompleted(eventName, jobId, endpointId);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return new WebhookDeliveryHandle(jobId);
        }
        catch(Exception ex) {
            WebhookMeter.DispatchErrorCount.Add(1, new TagList {
                { "webhook.endpoint_id", endpointId.Value },
                { "webhook.event_name", eventName }
            });

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            this._logger.LogDispatchFailed(ex, eventName, endpointId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        WebhookJobRecord? jobRecord = await this._store.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Cannot replay non-existent job '{jobId}'.");

        jobRecord.Status = WebhookJobStatus.Queued;
        await this._store.UpdateStatusAsync(jobId, WebhookJobStatus.Queued, cancellationToken).ConfigureAwait(false);
         
        IWebhookEvent payload = new RawJsonWebhookEvent(jobRecord.EventType, jobRecord.SerializedPayload);

        WebhookDeliveryJob job = new(jobId, jobRecord.EndpointId, payload);
        await this._transport.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);

        return new WebhookDeliveryHandle(jobId);
    }

    private sealed record RawJsonWebhookEvent(string EventType, string SerializedJson) : IWebhookEvent {
        public static string EventName => "raw.json";
        public override string ToString() {
            return this.SerializedJson;
        }
    }
}