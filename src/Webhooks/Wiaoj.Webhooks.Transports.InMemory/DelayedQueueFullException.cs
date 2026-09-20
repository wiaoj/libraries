namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// Thrown when a job is scheduled with a delay while the delayed queue is full and the configured policy is
/// <see cref="DelayedQueueOverflowPolicy.Reject"/>.
/// </summary>
public sealed class DelayedQueueFullException : Exception {
    /// <summary>Initializes a new instance of the <see cref="DelayedQueueFullException"/> class.</summary>
    /// <param name="jobId">The job that was refused.</param>
    /// <param name="capacity">The capacity the queue is held at.</param>
    public DelayedQueueFullException(WebhookJobId jobId, int capacity)
        : base($"The delayed queue is full ({capacity} jobs); job '{jobId}' was not scheduled.") {
        this.JobId = jobId;
        this.Capacity = capacity;
    }

    /// <summary>Gets the job that was refused.</summary>
    public WebhookJobId JobId { get; }

    /// <summary>Gets the capacity the queue is held at.</summary>
    public int Capacity { get; }
}
