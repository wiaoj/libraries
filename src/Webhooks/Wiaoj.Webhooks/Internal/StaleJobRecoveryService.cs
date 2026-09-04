using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Resilient background service that periodically sweeps and recovers abandoned in-flight, stranded queued,
/// and orphaned retrying webhook jobs caused by sudden process termination, OOM kills, or unhandled worker crashes.
/// </summary>
internal sealed class StaleJobRecoveryService : BackgroundService {
    private readonly IWebhookStore _store;
    private readonly IWebhookTransport _transport;
    private readonly IWebhookEventRegistry _eventRegistry;
    private readonly ISerializer<WebhookSerializerKey> _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly WebhookRecoveryOptions _options;
    private readonly string _instanceId;
    private readonly ILogger<StaleJobRecoveryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaleJobRecoveryService"/> class.
    /// </summary>
    public StaleJobRecoveryService(
        IWebhookStore store,
        IWebhookTransport transport,
        IWebhookEventRegistry eventRegistry,
        ISerializer<WebhookSerializerKey> serializer,
        TimeProvider timeProvider,
        IOptions<WebhookOptions> webhookOptions,
        IOptions<WebhookRecoveryOptions> recoveryOptions,
        ILogger<StaleJobRecoveryService> logger) {

        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(transport);
        Preca.ThrowIfNull(eventRegistry);
        Preca.ThrowIfNull(serializer);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(webhookOptions);
        Preca.ThrowIfNull(recoveryOptions);
        Preca.ThrowIfNull(logger);

        recoveryOptions.Value.Validate();

        this._store = store;
        this._transport = transport;
        this._eventRegistry = eventRegistry;
        this._serializer = serializer;
        this._timeProvider = timeProvider;
        this._options = recoveryOptions.Value;
        this._instanceId = webhookOptions.Value.InstanceId;
        this._logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using PeriodicTimer timer = new(this._options.PollingInterval, this._timeProvider);

        while(!stoppingToken.IsCancellationRequested) {
            try {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                await SweepAndRecoverAsync(stoppingToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                break;
            }
            catch(Exception ex) {
                this._logger.LogRecoverySweepFailed(ex);
            }
        }
    }

    /// <summary>
    /// Executes a single recovery sweep batch for expired in-flight, stranded queued, and orphaned retrying jobs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of stale jobs successfully recovered.</returns>
    public async Task<int> SweepAndRecoverAsync(CancellationToken cancellationToken = default) {
        long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset queuedThreshold = now.Subtract(this._options.QueuedJobStaleThreshold);

        this._logger.LogRecoverySweepStarting(now);

        IReadOnlyList<WebhookJobRecord> staleJobs = await this._store.GetStaleJobsAsync(
            now,
            queuedThreshold,
            now,
            this._options.BatchSize,
            cancellationToken).ConfigureAwait(false);

        if(staleJobs.Count == 0) {
            double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            WebhookMeter.RecoverySweepDuration.Record(elapsedMs);
            return 0;
        }

        int recoveredCount = 0;

        foreach(WebhookJobRecord job in staleJobs) {
            if(cancellationToken.IsCancellationRequested) {
                break;
            }

            bool leaseClaimed = await this._store.TryClaimLeaseAsync(
                job.Id,
                this._instanceId,
                this._options.RecoveryLeaseDuration,
                cancellationToken).ConfigureAwait(false);

            if(!leaseClaimed) {
                continue;
            }

            try {
                if(!this._eventRegistry.TryGetEventType(job.EventType, out Type? eventType) || eventType is null) {
                    this._logger.LogEndpointResolutionFailed(null, job.Id, job.EndpointId);
                    await this._store.UpdateStatusAsync(job.Id, WebhookJobStatus.DeadLettered, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                object? deserialized = this._serializer.DeserializeFromString(job.SerializedPayload, eventType);
                if(deserialized is not IWebhookEvent domainEvent) {
                    this._logger.LogEndpointResolutionFailed(null, job.Id, job.EndpointId);
                    await this._store.UpdateStatusAsync(job.Id, WebhookJobStatus.DeadLettered, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                WebhookDeliveryJob deliveryJob = new(
                    job.Id,
                    job.EndpointId,
                    WebhookPartitionKey.Parse(job.PartitionKey),
                    job.EventType,
                    domainEvent);

                await this._store.UpdateStatusAsync(job.Id, WebhookJobStatus.Queued, cancellationToken).ConfigureAwait(false);
                await this._transport.EnqueueAsync(deliveryJob, cancellationToken).ConfigureAwait(false);

                recoveredCount++;
            }
            catch(Exception ex) {
                this._logger.LogRecoverySweepFailed(ex);
            }
        }

        double durationMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        WebhookMeter.RecoverySweepDuration.Record(durationMs);

        if(recoveredCount > 0) {
            WebhookMeter.RecoveredJobsCount.Add(recoveredCount);
            this._logger.LogRecoverySweepCompleted(recoveredCount);
        }

        return recoveredCount;
    }
}