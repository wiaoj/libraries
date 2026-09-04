namespace Wiaoj.Webhooks;

/// <summary>
/// Contract for persisting webhook jobs, execution state transitions, lease locking, and delivery attempt history.
/// </summary>
public interface IWebhookStore {
    /// <summary>
    /// Persists a newly created webhook job.
    /// </summary>
    /// <param name="job">The job record to store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveAsync(WebhookJobRecord job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of newly created webhook jobs in a single atomic database operation.
    /// </summary>
    /// <param name="jobs">The collection of job records to persist.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveBatchAsync(IReadOnlyList<WebhookJobRecord> jobs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a webhook job record by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The job record if found; otherwise, <see langword="null"/>.</returns>
    Task<WebhookJobRecord?> GetJobAsync(WebhookJobId jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the historical webhook job records for a specific endpoint.
    /// </summary>
    /// <param name="endpointId">The endpoint identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A read-only collection of historical job records for the endpoint.</returns>
    Task<IReadOnlyList<WebhookJobRecord>> GetHistoryByEndpointAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the lifecycle status of a webhook job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="status">The new status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the lifecycle status and next scheduled retry timestamp of a webhook job atomically.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="status">The new status.</param>
    /// <param name="nextAttemptAt">The timestamp when the next execution attempt should occur. Pass <see langword="null"/> to clear the scheduled retry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire an execution lease lock for a worker instance.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="instanceId">The identifier of the acquiring instance/pod.</param>
    /// <param name="duration">The duration of the lease.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the lease was successfully claimed; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryClaimLeaseAsync(WebhookJobId jobId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new delivery attempt outcome for a webhook job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="attempt">The delivery attempt details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RecordAttemptAsync(WebhookJobId jobId, WebhookDeliveryAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves stale jobs eligible for recovery, filtering expired in-flight executions, stranded queued jobs,
    /// and orphaned retrying jobs whose scheduled retry time has passed.
    /// </summary>
    /// <param name="inFlightThreshold">The optional cutoff timestamp for in-flight lease expiration. When <see langword="null"/>, in-flight jobs are excluded.</param>
    /// <param name="queuedThreshold">The optional cutoff timestamp for stranded queued jobs. When <see langword="null"/>, queued jobs are excluded.</param>
    /// <param name="retryingDueThreshold">The optional cutoff timestamp for orphaned retrying jobs whose <see cref="WebhookJobRecord.NextAttemptAt"/> has passed. When <see langword="null"/>, retrying jobs are excluded.</param>
    /// <param name="maxCount">The maximum number of stale jobs to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of stale jobs available for recovery.</returns>
    Task<IReadOnlyList<WebhookJobRecord>> GetStaleJobsAsync(
        DateTimeOffset? inFlightThreshold,
        DateTimeOffset? queuedThreshold,
        DateTimeOffset? retryingDueThreshold,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a collection of dead-lettered jobs for administrative inspection or replay.
    /// </summary>
    /// <param name="maxCount">The maximum number of dead-lettered jobs to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A read-only collection of dead-lettered job records.</returns>
    Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a collection of dead-lettered jobs for a specific endpoint.
    /// </summary>
    /// <param name="endpointId">The endpoint identifier.</param>
    /// <param name="maxCount">The maximum number of dead-lettered jobs to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A read-only collection of dead-lettered job records for the endpoint.</returns>
    Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(WebhookEndpointId endpointId, int maxCount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain-focused extension methods for <see cref="IWebhookStore"/> providing clear, intention-revealing recovery and audit queries.
/// </summary>
public static class WebhookStoreExtensions {
    /// <summary>
    /// Retrieves in-flight jobs whose execution lease lock has expired before the specified threshold.
    /// Excludes queued and retrying jobs.
    /// </summary>
    /// <param name="store">The webhook store instance.</param>
    /// <param name="leaseExpirationThreshold">The cutoff timestamp for expired leases.</param>
    /// <param name="maxCount">The maximum number of jobs to retrieve.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A read-only collection of expired in-flight jobs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public static Task<IReadOnlyList<WebhookJobRecord>> GetExpiredInFlightJobsAsync(
        this IWebhookStore store,
        DateTimeOffset leaseExpirationThreshold,
        int maxCount,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(store);
        return store.GetStaleJobsAsync(leaseExpirationThreshold, null, null, maxCount, cancellationToken);
    }

    /// <summary>
    /// Retrieves stranded queued jobs that were created before the specified threshold and never picked up by a worker.
    /// Excludes in-flight and retrying jobs.
    /// </summary>
    /// <param name="store">The webhook store instance.</param>
    /// <param name="createdBeforeThreshold">The cutoff timestamp for stranded queued jobs.</param>
    /// <param name="maxCount">The maximum number of jobs to retrieve.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A read-only collection of stranded queued jobs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public static Task<IReadOnlyList<WebhookJobRecord>> GetStrandedQueuedJobsAsync(
        this IWebhookStore store,
        DateTimeOffset createdBeforeThreshold,
        int maxCount,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(store);
        return store.GetStaleJobsAsync(null, createdBeforeThreshold, null, maxCount, cancellationToken);
    }

    /// <summary>
    /// Retrieves orphaned retrying jobs whose scheduled <see cref="WebhookJobRecord.NextAttemptAt"/> has passed
    /// and are no longer held by an in-memory delayed scheduler (e.g. due to node crash or restart).
    /// Excludes in-flight and queued jobs.
    /// </summary>
    /// <param name="store">The webhook store instance.</param>
    /// <param name="dueThreshold">The cutoff timestamp. Jobs with <see cref="WebhookJobRecord.NextAttemptAt"/> at or before this value are returned.</param>
    /// <param name="maxCount">The maximum number of jobs to retrieve.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A read-only collection of orphaned retrying jobs eligible for recovery.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public static Task<IReadOnlyList<WebhookJobRecord>> GetOrphanedRetryingJobsAsync(
        this IWebhookStore store,
        DateTimeOffset dueThreshold,
        int maxCount,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(store);
        return store.GetStaleJobsAsync(null, null, dueThreshold, maxCount, cancellationToken);
    }
}