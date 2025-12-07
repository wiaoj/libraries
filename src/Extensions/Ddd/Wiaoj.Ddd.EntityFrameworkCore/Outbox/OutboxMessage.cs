using System.ComponentModel.DataAnnotations;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;
public sealed class OutboxMessage {
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    public string? PartitionKey { get; private set; }

    // Pessimistic Lock Fields
    public string? LockId { get; private set; }
    public DateTimeOffset? LockExpiration { get; private set; }

    [ConcurrencyCheck]
    public Guid Version { get; private set; }

#pragma warning disable CS8618
    private OutboxMessage() { }
#pragma warning restore CS8618

    public OutboxMessage(
        Guid id,
        string type,
        string content,
        DateTimeOffset occurredAt,
        string? partitionKey,
        string lockId,
        DateTimeOffset lockExpiration) {

        this.Id = id;
        this.Type = type;
        this.Content = content;
        this.OccurredAt = occurredAt;
        this.PartitionKey = partitionKey;

        // Initial Lock Ownership
        this.LockId = lockId;
        this.LockExpiration = lockExpiration;

        this.Version = Guid.NewGuid();
    }

    public void MarkAsProcessed(DateTimeOffset processedAt) {
        this.ProcessedAt = processedAt;
        this.Error = null;
        this.Version = Guid.NewGuid();
    }

    public void MarkAsFailed(string error) {
        this.Error = error;
        this.RetryCount++;
        this.Version = Guid.NewGuid();
    }
}