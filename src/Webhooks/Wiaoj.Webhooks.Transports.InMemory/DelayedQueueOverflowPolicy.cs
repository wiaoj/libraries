namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// What the in-memory delayed scheduler does with a job scheduled while its queue is at
/// <see cref="InMemoryWebhookTransportOptions.MaxDelayedCapacity"/>.
/// </summary>
/// <remarks>
/// The delayed queue holds retries waiting for their backoff to expire. A long outage at the destination fills it, and
/// an unbounded queue turns that outage into an out-of-memory crash, losing everything it held.
/// </remarks>
public enum DelayedQueueOverflowPolicy {
    /// <summary>
    /// Refuse the job: <c>EnqueueAsync</c> throws <see cref="DelayedQueueFullException"/>, and the caller decides.
    /// </summary>
    Reject = 0,

    /// <summary>
    /// Admit the job and drop the ones due furthest in the future, keeping the queue at its capacity. The most urgent
    /// retries survive; the dropped jobs are recovered from the store by the stale job recovery service.
    /// </summary>
    DropOldest = 1,

    /// <summary>
    /// Leave the job out of memory. It keeps whatever state the store holds — a retry is already persisted as
    /// <see cref="WebhookJobStatus.Retrying"/> with its <see cref="WebhookJobRecord.NextAttemptAt"/> — and the stale job
    /// recovery service enqueues it when it comes due.
    /// </summary>
    /// <remarks>
    /// This is the default, and it depends on recovery being enabled (<c>UseStaleJobRecovery()</c>). Without it, a job
    /// dropped here is never delivered.
    /// </remarks>
    PersistOnlyFallback = 2
}
