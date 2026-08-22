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
    /// Retrieves stale jobs that are still marked as in-flight but whose lease lock has expired.
    /// </summary>
    /// <param name="threshold">The cutoff timestamp.</param>
    /// <param name="maxCount">The maximum number of stale jobs to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of stale in-flight jobs available for recovery.</returns>
    Task<IReadOnlyList<WebhookJobRecord>> GetStaleInFlightJobsAsync(DateTimeOffset threshold, int maxCount, CancellationToken cancellationToken = default);
      
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