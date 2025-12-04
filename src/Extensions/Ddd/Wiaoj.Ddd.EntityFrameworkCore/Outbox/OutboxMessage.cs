namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;

public sealed class OutboxMessage {
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

#pragma warning disable CS8618
    private OutboxMessage() { }
#pragma warning restore CS8618

    public OutboxMessage(Guid id, string type, string content, DateTimeOffset occurredAt) {
        this.Id = id;
        this.Type = type;
        this.Content = content;
        this.OccurredAt = occurredAt;
    }

    public void MarkAsProcessed(DateTimeOffset processedAt) {
        this.ProcessedAt = processedAt;
        this.Error = null;
    }

    public void MarkAsFailed(string error) {
        this.Error = error;
        this.RetryCount++;
    }
}