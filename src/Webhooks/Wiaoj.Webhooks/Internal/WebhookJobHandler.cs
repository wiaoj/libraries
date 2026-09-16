using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Default <see cref="IWebhookJobHandler"/> implementation. Resolves the job's endpoint,
/// serializes the payload, runs the pipeline, and updates the persistent store.
/// </summary>
internal sealed class WebhookJobHandler : IWebhookJobHandler {
    private readonly IWebhookStore _store;
    private readonly IWebhookEndpointResolver _endpointResolver;
    private readonly ISerializer<WebhookSerializerKey> _serializer;
    private readonly WebhookPipelineRunner _pipelineRunner;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookJobHandler> _logger;
    private readonly WebhookJobExecutionGuard _guard;
    private readonly string _instanceId;
    private readonly TimeSpan _leaseDuration;

    public WebhookJobHandler(
        IWebhookStore store,
        IWebhookEndpointResolver endpointResolver,
        ISerializer<WebhookSerializerKey> serializer,
        WebhookPipelineRunner pipelineRunner,
        TimeProvider timeProvider,
        ILogger<WebhookJobHandler> logger)
        : this(
            store,
            endpointResolver,
            serializer,
            pipelineRunner,
            timeProvider,
            logger,
            new WebhookJobExecutionGuard(),
            Options.Create(new WebhookOptions()),
            Options.Create(new WebhookRecoveryOptions())) {
    }

    public WebhookJobHandler(
        IWebhookStore store,
        IWebhookEndpointResolver endpointResolver,
        ISerializer<WebhookSerializerKey> serializer,
        WebhookPipelineRunner pipelineRunner,
        TimeProvider timeProvider,
        ILogger<WebhookJobHandler> logger,
        WebhookJobExecutionGuard guard,
        IOptions<WebhookOptions> webhookOptions,
        IOptions<WebhookRecoveryOptions> recoveryOptions) {
        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(endpointResolver);
        Preca.ThrowIfNull(serializer);
        Preca.ThrowIfNull(pipelineRunner);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);
        Preca.ThrowIfNull(guard);
        Preca.ThrowIfNull(webhookOptions);
        Preca.ThrowIfNull(recoveryOptions);

        this._store = store;
        this._endpointResolver = endpointResolver;
        this._serializer = serializer;
        this._pipelineRunner = pipelineRunner;
        this._timeProvider = timeProvider;
        this._logger = logger;
        this._guard = guard;
        this._instanceId = webhookOptions.Value.InstanceId;
        this._leaseDuration = recoveryOptions.Value.RecoveryLeaseDuration;
    }

    public async Task<WebhookDeliveryAttempt> HandleAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);

        // A job can reach a worker more than once: a delayed retry and a copy re-enqueued by recovery, or two recovery
        // sweeps. Copies in this process take turns; across instances the lease decides. A copy that cannot lease the
        // job — leased elsewhere, or already finished — is not delivered.
        using WebhookJobExecutionGuard.Releaser slot = await this._guard.EnterAsync(job.Id, cancellationToken);

        if(!await this._store.TryClaimLeaseAsync(job.Id, this._instanceId, this._leaseDuration, cancellationToken)) {
            WebhookJobRecord? record = await this._store.GetJobAsync(job.Id, cancellationToken);
            if(record is not null) {
                this._logger.LogJobDeliverySkipped(job.Id, record.Status, record.LockedBy);
                return WebhookDeliveryAttempt.Create(
                    job.EndpointId,
                    record.Attempts.Count + 1,
                    UnixTimestamp.From(this._timeProvider),
                    TimeSpan.Zero,
                    WebhookDeliveryResult.Duplicate($"job-lease:{job.Id}"));
            }

            // The store does not track this job, so there is no lease to take.
        }

        return await DeliverAsync(job, cancellationToken);
    }

    private async Task<WebhookDeliveryAttempt> DeliverAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) {
        WebhookEndpoint? endpoint;
        try {
            endpoint = await this._endpointResolver.ResolveAsync(job.EndpointId, cancellationToken);
        }
        catch(Exception ex) {
            this._logger.LogEndpointResolutionFailed(ex, job.Id, job.EndpointId);
            throw;
        }

        if(endpoint is null) {
            this._logger.LogEndpointResolutionFailed(null, job.Id, job.EndpointId);
            throw new WebhookEndpointNotFoundException(job.EndpointId);
        }

        this._logger.LogEndpointResolved(job.EndpointId, endpoint.TargetUrl);

        string serializedPayload = this._serializer.SerializeToString(job.Payload, job.Payload.GetType());

        WebhookJobRecord? existingJob = await this._store.GetJobAsync(job.Id, cancellationToken);
        List<WebhookDeliveryAttempt> priorAttempts = existingJob is not null ? [.. existingJob.Attempts] : [];

        WebhookDeliveryContext context = new() {
            JobId = job.Id,
            Endpoint = endpoint,
            PartitionKey = job.PartitionKey,
            EventType = job.EventType,
            Event = job.Payload,
            SerializedPayload = serializedPayload,
            AttemptHistory = priorAttempts
        };

        if(job.IsReplay) {
            context.MarkReplay(true);
        }

        WebhookDeliveryAttempt attempt = await this._pipelineRunner.RunAsync(context, cancellationToken);

        await this._store.RecordAttemptAsync(job.Id, attempt, cancellationToken);
        this._logger.LogStoreAttemptRecorded(job.Id, attempt.AttemptNumber, attempt.IsSuccess, attempt.Duration.TotalMilliseconds);
         
        bool isDeadLettered = context.IsDeadLettered();

        WebhookJobStatus newStatus = attempt.Result switch {
            WebhookDeliveryResult.Delivered or WebhookDeliveryResult.Deduplicated => WebhookJobStatus.Delivered,
            WebhookDeliveryResult.TransientFailure when !isDeadLettered => WebhookJobStatus.Retrying,
            _ => WebhookJobStatus.DeadLettered
        };

        if(newStatus == WebhookJobStatus.Retrying) {
            TimeSpan? retryDelay = context.GetScheduledRetryDelay();
            TimeSpan delay = retryDelay.HasValue && retryDelay.Value > TimeSpan.Zero
                ? retryDelay.Value
                : TimeSpan.FromSeconds(30);
            DateTimeOffset nextAttemptAt = this._timeProvider.GetUtcNow().Add(delay);
            await this._store.UpdateStatusAsync(job.Id, newStatus, nextAttemptAt, cancellationToken);
        }
        else {
            await this._store.UpdateStatusAsync(job.Id, newStatus, cancellationToken);
        }

        this._logger.LogStoreStatusUpdated(job.Id, newStatus);

        int? statusCode = attempt.Result switch {
            WebhookDeliveryResult.Delivered d => d.StatusCode,
            WebhookDeliveryResult.TransientFailure tf => tf.StatusCode,
            WebhookDeliveryResult.PermanentFailure pf => pf.StatusCode,
            _ => null
        };

        string? errorMessage = attempt.Result switch {
            WebhookDeliveryResult.TransientFailure tf => tf.ErrorMessage,
            WebhookDeliveryResult.PermanentFailure pf => pf.ErrorMessage,
            _ => null
        };

        if(attempt.IsSuccess) {
            this._logger.LogDeliverySuccess(job.Id, attempt.AttemptNumber, job.EndpointId, statusCode, attempt.Duration.TotalMilliseconds);
        }
        else {
            this._logger.LogDeliveryAttemptWarning(job.Id, attempt.AttemptNumber, job.EndpointId, statusCode, errorMessage, attempt.Duration.TotalMilliseconds);
        }

        return attempt;
    } 
}