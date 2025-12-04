namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox; 
public sealed class OutboxOptions {
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 20;
    public int RetryCount { get; set; } = 3;
}