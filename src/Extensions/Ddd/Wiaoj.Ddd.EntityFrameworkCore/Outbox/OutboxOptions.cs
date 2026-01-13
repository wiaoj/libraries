namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;
public sealed record OutboxOptions {
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int BatchSize { get; set; } = 20;
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of recursive domain event dispatch iterations allowed.
    /// Default is 10.
    /// </summary>
    public int MaxDomainEventDispatchAttempts { get; set; } = 10;

    /// <summary>
    /// If set, this instance will only poll messages with this PartitionKey.
    /// If null, it processes all messages (or messages without a key, depending on logic).
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the duration for which a message remains locked by an instance.
    /// If processing takes longer than this, another instance may claim the message.
    /// Default is 60 seconds.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the initial delay before the processor starts polling the database.
    /// Useful for waiting for database migrations or application warmup.
    /// Default is TimeSpan.Zero.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(2);
}