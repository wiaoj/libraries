using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Resilient background service that periodically sweeps and recovers abandoned in-flight webhook jobs
/// caused by sudden process termination, OOM kills, or unhandled worker crashes.
/// </summary>
internal sealed class StaleJobRecoveryService : BackgroundService {
    private readonly IWebhookStore _store;
    private readonly IWebhookTransport _transport;
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
        TimeProvider timeProvider,
        IOptions<WebhookRecoveryOptions> options,
        ILogger<StaleJobRecoveryService> logger) {
        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(transport);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        options.Value.Validate();

        this._store = store;
        this._transport = transport;
        this._timeProvider = timeProvider;
        this._options = options.Value;
        this._logger = logger;
        this._instanceId = $"recovery-worker-{Guid.NewGuid():N}";
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
    /// Executes a single recovery sweep batch.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of stale jobs successfully recovered.</returns>
    public async Task<int> SweepAndRecoverAsync(CancellationToken cancellationToken = default) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        this._logger.LogRecoverySweepStarting(now);

        IReadOnlyList<WebhookJobRecord> staleJobs = await this._store.GetStaleInFlightJobsAsync(
            now,
            this._options.BatchSize,
            cancellationToken).ConfigureAwait(false);

        if(staleJobs.Count == 0) {
            return 0;
        }

        int recoveredCount = 0;

        foreach(WebhookJobRecord job in staleJobs) {
            if(cancellationToken.IsCancellationRequested) {
                break;
            }

            // Distributed race protection: Only the worker that successfully claims the lease recovers the job
            bool leaseClaimed = await this._store.TryClaimLeaseAsync(
                job.Id,
                this._instanceId,
                this._options.RecoveryLeaseDuration,
                cancellationToken).ConfigureAwait(false);

            if(!leaseClaimed) {
                continue;
            }

            try {
                // Reconstruct delivery payload from persistent raw JSON
                IWebhookEvent payload = new RawJsonWebhookEvent(job.EventType, job.SerializedPayload);
                WebhookDeliveryJob deliveryJob = new(job.Id,
                                                     job.EndpointId,
                                                     job.PartitionKey,
                                                     job.EventType,
                                                     payload);

                // Transition status back to Queued
                await this._store.UpdateStatusAsync(job.Id, WebhookJobStatus.Queued, cancellationToken).ConfigureAwait(false);

                // Re-enqueue into the execution transport channel
                await this._transport.EnqueueAsync(deliveryJob, cancellationToken).ConfigureAwait(false);

                recoveredCount++;
            }
            catch(Exception ex) {
                this._logger.LogRecoverySweepFailed(ex);
            }
        }

        if(recoveredCount > 0) {
            this._logger.LogRecoverySweepCompleted(recoveredCount);
        }

        return recoveredCount;
    }
}