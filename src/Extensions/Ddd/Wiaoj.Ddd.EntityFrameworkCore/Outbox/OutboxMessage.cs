using System.ComponentModel.DataAnnotations;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox; 
/// <summary>
/// Represents a message stored in the database as part of the Transactional Outbox pattern.
/// This ensures exactly-once or at-least-once delivery of domain events by saving them
/// within the same database transaction as the business state changes.
/// </summary>
public sealed class OutboxMessage { 
    /// <summary>
    /// Gets the unique identifier of the outbox message.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the fully qualified assembly name or a custom identifier for the event type.
    /// Used to deserialize the <see cref="Content"/> back into the correct CLR object.
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    /// Gets the serialized payload of the domain event, typically in JSON format.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Gets the timestamp indicating when the domain event originally occurred.
    /// Used for ordering and chronologically processing messages.
    /// </summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Gets the timestamp indicating when the message was successfully processed.
    /// If this property is null, the message is pending or has failed and needs to be retried.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the processor instance (e.g., Pod Name, Machine Name, or a Guid) 
    /// that successfully processed this message. Useful for auditing and debugging.
    /// </summary>
    public string? ProcessedBy { get; private set; }

    /// <summary>
    /// Gets the error message or exception details if the message processing failed.
    /// This is reset to null if a subsequent retry is successful.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets the number of times the system has attempted to process this message and failed.
    /// Used to stop processing after a certain threshold (e.g., dead-lettering).
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Gets an optional key used to group related messages. 
    /// Messages with the same partition key can be routed to the same processor instance 
    /// to guarantee strict sequential ordering.
    /// </summary>
    public string? PartitionKey { get; private set; }

    /// <summary>
    /// Gets the identifier of the processor instance currently holding a pessimistic lock on this message.
    /// If null, the message is available to be picked up by any polling instance.
    /// </summary>
    public string? LockId { get; private set; }

    /// <summary>
    /// Gets the timestamp when the current pessimistic lock expires. 
    /// If this time has passed and the message is still not processed, it is considered a "zombie" 
    /// and can be reclaimed by another processor.
    /// </summary>
    public DateTimeOffset? LockExpiration { get; private set; }

    /// <summary>
    /// Gets the optimistic concurrency token used by Entity Framework Core.
    /// Ensures that two different instances do not update the same message simultaneously.
    /// </summary>
    [ConcurrencyCheck]
    public Guid Version { get; private set; }

#pragma warning disable CS8618
    /// <summary>
    /// Parameterless constructor required by Entity Framework Core for materialization.
    /// </summary>
    private OutboxMessage() { }
#pragma warning restore CS8618

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxMessage"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the message.</param>
    /// <param name="type">The type name of the serialized event.</param>
    /// <param name="content">The serialized event payload.</param>
    /// <param name="occurredAt">The timestamp when the event occurred.</param>
    /// <param name="partitionKey">The partition key for sequential processing routing.</param>
    /// <param name="lockId">The initial processor instance acquiring the lock.</param>
    /// <param name="lockExpiration">The initial lock expiration timestamp.</param>
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

        // Initial Lock Ownership via Fast-Path (In-Memory Channel)
        this.LockId = lockId;
        this.LockExpiration = lockExpiration;

        this.Version = Guid.NewGuid();
    }

    /// <summary>
    /// Marks the outbox message as successfully processed. 
    /// Sets the completion timestamp, records the processing instance, clears any errors, 
    /// and releases the pessimistic lock.
    /// </summary>
    /// <param name="processedAt">The timestamp when the processing completed.</param>
    /// <param name="processedBy">The identifier of the instance that completed the processing.</param>
    public void MarkAsProcessed(DateTimeOffset processedAt, string processedBy) {
        this.ProcessedAt = processedAt;
        this.ProcessedBy = processedBy;
        this.Error = null;

        // Release the lock
        this.LockId = null;
        this.LockExpiration = null;

        // Update concurrency token
        this.Version = Guid.NewGuid();
    }

    /// <summary>
    /// Marks the outbox message as failed. 
    /// Increments the retry count, records the error details, and releases the pessimistic lock 
    /// so the message can be picked up again in the next polling cycle.
    /// </summary>
    /// <param name="error">The exception message or error details.</param>
    public void MarkAsFailed(string error) {
        this.Error = error;
        this.RetryCount++;

        // Release the lock so it can be retried by other workers without waiting for expiration
        this.LockId = null;
        this.LockExpiration = null;

        // Update concurrency token
        this.Version = Guid.NewGuid();
    }
}