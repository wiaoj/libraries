#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for the background stale in-flight job recovery service.
/// </summary>
public sealed class WebhookRecoveryOptions {
    /// <summary>
    /// The default polling interval between recovery sweeps (30 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The default maximum number of stale jobs retrieved per sweep batch (100).
    /// </summary>
    public const int DefaultBatchSize = 100;

    /// <summary>
    /// The default lease lock duration claimed when recovering a stale job (2 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultRecoveryLeaseDuration = TimeSpan.FromMinutes(2);

    /// <summary>The default threshold after which an unprocessed queued job is considered a stranded zombie (2 minutes).</summary>
    public static readonly TimeSpan DefaultQueuedJobStaleThreshold = TimeSpan.FromMinutes(2);
  
    /// <summary>
    /// Gets or sets the interval between periodic recovery sweeps. Default is 30 seconds.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;

    /// <summary>
    /// Gets or sets the maximum number of stale jobs to process in a single recovery batch. Default is 100.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>
    /// Gets or sets the lease lock duration when claiming a stale job for re-enqueuing. Default is 2 minutes.
    /// </summary>
    public TimeSpan RecoveryLeaseDuration { get; set; } = DefaultRecoveryLeaseDuration;

    /// <summary>
    /// Gets or sets the duration an unprocessed job may remain in <see cref="WebhookJobStatus.Queued"/> state
    /// before being classified as a stranded zombie job eligible for recovery. Default is 2 minutes.
    /// </summary>
    public TimeSpan QueuedJobStaleThreshold { get; set; } = DefaultQueuedJobStaleThreshold;

    /// <summary>
    /// Gets or sets an optional grace period buffer subtracted from the current time when evaluating orphaned retrying jobs.
    /// Default is <see cref="TimeSpan.Zero"/> (jobs whose <see cref="WebhookJobRecord.NextAttemptAt"/> is at or before the current timestamp are recovered).
    /// </summary>
    public TimeSpan RetryingJobGracePeriod { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration value is negative or out of bounds.</exception>
    public void Validate() {
        if(this.PollingInterval <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.PollingInterval), "Polling interval must be a positive non-zero duration.");
        }
        if(this.RecoveryLeaseDuration <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.RecoveryLeaseDuration), "Recovery lease duration must be a positive non-zero duration.");
        }
        if(this.QueuedJobStaleThreshold <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.QueuedJobStaleThreshold), "Queued job stale threshold must be a positive non-zero duration.");
        }
        if(this.RetryingJobGracePeriod < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.RetryingJobGracePeriod), "Retrying job grace period cannot be negative.");
        }
        Preca.ThrowIfLessThan(this.BatchSize, 1);
    }
}