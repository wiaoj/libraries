#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Configuration options for the background retention pruning service.
/// </summary>
public sealed class WebhookRetentionOptions {
    /// <summary>
    /// The default polling interval between retention sweeps (1 hour).
    /// </summary>
    public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// The default retention window for delivered jobs (7 days).
    /// </summary>
    public static readonly TimeSpan DefaultDeliveredRetention = TimeSpan.FromDays(7);

    /// <summary>
    /// The default retention window for dead-lettered jobs (30 days).
    /// </summary>
    public static readonly TimeSpan DefaultDeadLetterRetention = TimeSpan.FromDays(30);

    /// <summary>
    /// The default maximum number of jobs deleted per prune batch (1000).
    /// </summary>
    public const int DefaultBatchSize = 1000;

    /// <summary>
    /// Gets or sets the interval between periodic retention sweeps. Default is 1 hour.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;

    /// <summary>
    /// Gets or sets how long a <see cref="WebhookJobStatus.Delivered"/> job is kept after completion
    /// before it becomes eligible for pruning. Default is 7 days.
    /// </summary>
    public TimeSpan DeliveredRetention { get; set; } = DefaultDeliveredRetention;

    /// <summary>
    /// Gets or sets how long a <see cref="WebhookJobStatus.DeadLettered"/> job is kept after completion
    /// before it becomes eligible for pruning. Default is 30 days.
    /// </summary>
    public TimeSpan DeadLetterRetention { get; set; } = DefaultDeadLetterRetention;

    /// <summary>
    /// Gets or sets the maximum number of jobs deleted in a single prune batch, bounding the database
    /// transaction size. Default is 1000.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration value is negative or out of bounds.</exception>
    public void Validate() {
        if(this.PollingInterval <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.PollingInterval), "Polling interval must be a positive non-zero duration.");
        }
        if(this.DeliveredRetention < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.DeliveredRetention), "Delivered retention cannot be negative.");
        }
        if(this.DeadLetterRetention < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.DeadLetterRetention), "Dead-letter retention cannot be negative.");
        }
        if(this.BatchSize < 1) {
            throw new ArgumentOutOfRangeException(nameof(this.BatchSize), this.BatchSize, "Batch size must be at least 1.");
        }
    }
}
