using Wiaoj.Preconditions;
namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;

/// <summary>
/// Controls how a failed delivery is retried before it is given up on.
/// </summary>
public sealed record OutboxRetryPolicy {
    /// <summary>Gets or sets the delay before the first retry. Default is 5 seconds.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the multiplier applied to the delay after each failure. Default is 2.</summary>
    public double BackoffFactor { get; set; } = 2.0;

    /// <summary>Gets or sets the ceiling on the retry delay. Default is 10 minutes.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the number of attempts after which a delivery is dead-lettered. Default is 5.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Calculates the delay before attempt number <paramref name="attemptsSoFar"/> + 1.</summary>
    public TimeSpan DelayFor(int attemptsSoFar) {
        double seconds = this.InitialDelay.TotalSeconds * Math.Pow(this.BackoffFactor, Math.Max(0, attemptsSoFar));
        return TimeSpan.FromSeconds(Math.Min(this.MaxDelay.TotalSeconds, seconds));
    }

    /// <summary>Validates the configuration values.</summary>
    public void Validate() {
        Preca.ThrowIfNegativeOrZero(this.InitialDelay);
        Preca.ThrowIfNegativeOrZero(this.MaxDelay);
        Preca.ThrowIfLessThan(this.MaxAttempts, 1);

        if(this.BackoffFactor < 1.0) {
            throw new ArgumentOutOfRangeException(nameof(this.BackoffFactor), "Backoff factor must be at least 1.0.");
        }
    }
}

/// <summary>
/// Runtime behaviour of the outbox processor. Schema shape lives in <c>ApplyDddOutbox</c> instead, because
/// it is fixed when the model is built.
/// </summary>
public sealed record OutboxOptions {
    /// <summary>Gets or sets how often the database is polled for claimable rows. Default is 10 seconds.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets how many rows a single claim takes. Default is 20.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Gets or sets the retry and dead-letter policy.</summary>
    public OutboxRetryPolicy RetryPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of recursive domain event dispatch iterations allowed. Default is 10.
    /// </summary>
    public int MaxDomainEventDispatchAttempts { get; set; } = 10;

    /// <summary>
    /// If set, this instance only claims rows carrying this partition key.
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets how long a claimed row stays locked. A processor that dies mid-dispatch releases its rows
    /// after this long. Default is 1 minute.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the delay before polling starts. Default is 2 minutes.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Validates the configuration values.</summary>
    public void Validate() {
        Preca.ThrowIfNegativeOrZero(this.PollingInterval);
        Preca.ThrowIfLessThan(this.BatchSize, 1);
        Preca.ThrowIfLessThan(this.MaxDomainEventDispatchAttempts, 1);
        Preca.ThrowIfNegativeOrZero(this.LockDuration);
        this.RetryPolicy.Validate();
    }
}
