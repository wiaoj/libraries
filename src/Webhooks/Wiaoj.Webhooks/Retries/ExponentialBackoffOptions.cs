namespace Wiaoj.Webhooks.Retries;

/// <summary>
/// Configuration options for <see cref="ExponentialBackoffPolicy"/>.
/// </summary>
public sealed class ExponentialBackoffOptions {
    /// <summary>
    /// The default maximum number of delivery attempts (including the initial attempt).
    /// </summary>
    public const int DefaultMaxAttempts = 5;

    /// <summary>
    /// The default initial retry delay before the first retry attempt.
    /// </summary>
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The default maximum retry delay cap.
    /// </summary>
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// The default backoff multiplier.
    /// </summary>
    public const double DefaultMultiplier = 2.0;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before giving up. Default is 5.
    /// </summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    /// <summary>
    /// Gets or sets the initial delay before the first retry. Default is 2 seconds.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = DefaultInitialDelay;

    /// <summary>
    /// Gets or sets the maximum delay cap between retries. Default is 1 hour.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = DefaultMaxDelay;

    /// <summary>
    /// Gets or sets the exponential backoff multiplier factor. Default is 2.0.
    /// </summary>
    public double Multiplier { get; set; } = DefaultMultiplier;

    /// <summary>
    /// Gets or sets the random jitter percentage applied to calculated retry delays.
    /// Default is <see cref="Wiaoj.Extensions.Jitter.Medium"/> (+/- 10%). Set to <see langword="null"/> or <see cref="Percentage.Zero"/> to disable jitter.
    /// </summary>
    public Percentage? Jitter { get; set; } = Wiaoj.Extensions.Jitter.Medium;

    /// <summary>
    /// Validates the configuration values, throwing an exception if any value is out of acceptable bounds.
    /// </summary>
    public void Validate() {
        Preca.ThrowIfLessThan(this.MaxAttempts, 1);
        Preca.ThrowIfLessThan(this.Multiplier, 1.0);
        if(this.InitialDelay < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.InitialDelay), "Initial delay cannot be negative.");
        }
        if(this.MaxDelay < this.InitialDelay) {
            throw new ArgumentOutOfRangeException(nameof(this.MaxDelay), "Max delay cannot be less than initial delay.");
        }
    }
}
