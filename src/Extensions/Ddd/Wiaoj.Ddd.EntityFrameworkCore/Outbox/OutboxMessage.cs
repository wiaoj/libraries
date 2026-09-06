using Wiaoj.Preconditions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;

/// <summary>
/// One durable unit of post-commit work: a single domain event destined for a single handler.
/// </summary>
/// <remarks>
/// <para>
/// <b>One row per (event, handler).</b> An event with five handlers produces five rows. Each is claimed,
/// retried and dead-lettered independently, so a handler that fails cannot force the four that already
/// succeeded to run again. The alternative — one row per event — makes the retry unit the event, which
/// silently requires every handler to be idempotent.
/// </para>
/// <para>
/// <b>Timestamps are UTC ticks.</b> <see cref="DateTimeOffset"/> columns are not orderable or comparable
/// on every provider (SQLite refuses both), and the claim query does nothing but order and compare on
/// them. Integer ticks are translatable everywhere and cheaper to index.
/// </para>
/// </remarks>
[DebuggerDisplay("{EventAlias,nq} -> {HandlerAlias,nq} (attempts={Attempts})")]
public sealed class OutboxMessage {
    /// <summary>Gets the unique identifier of this delivery.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the stable logical name of the event type, used to resolve the CLR type on dispatch.</summary>
    public string EventAlias { get; private set; }

    /// <summary>Gets the stable logical name of the handler this row is destined for.</summary>
    public string HandlerAlias { get; private set; }

    /// <summary>Gets the serialized event payload.</summary>
    public string Payload { get; private set; }

    /// <summary>Gets the optional key restricting which processor instance may claim this row.</summary>
    public string? PartitionKey { get; private set; }

    /// <summary>Gets the UTC ticks at which the event occurred.</summary>
    public long OccurredAtTicks { get; private set; }

    /// <summary>Gets the UTC ticks before which this row must not be claimed. Drives retry backoff.</summary>
    public long NextAttemptAtTicks { get; private set; }

    /// <summary>Gets the UTC ticks at which the handler completed successfully, or <see langword="null"/>.</summary>
    public long? ProcessedAtTicks { get; private set; }

    /// <summary>
    /// Gets the UTC ticks at which this row was given up on, or <see langword="null"/>. A dead-lettered row
    /// is terminal: it is never claimed again, and unlike an exhausted retry it says so.
    /// </summary>
    public long? DeadLetteredAtTicks { get; private set; }

    /// <summary>Gets the identifier of the processor instance that completed this row.</summary>
    public string? ProcessedBy { get; private set; }

    /// <summary>Gets the number of dispatch attempts made so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets the error recorded by the most recent failed attempt.</summary>
    public string? LastError { get; private set; }

    /// <summary>Gets the identifier of the processor instance currently holding this row.</summary>
    public string? LockId { get; private set; }

    /// <summary>Gets the UTC ticks at which the current lock expires.</summary>
    public long? LockExpiresAtTicks { get; private set; }

    /// <summary>Gets the moment the event occurred.</summary>
    [NotMapped]
    public DateTimeOffset OccurredAt => new(this.OccurredAtTicks, TimeSpan.Zero);

    /// <summary>Gets the moment this row completed successfully, if it has.</summary>
    [NotMapped]
    public DateTimeOffset? ProcessedAt => ToOffset(this.ProcessedAtTicks);

    /// <summary>Gets the moment this row was dead-lettered, if it was.</summary>
    [NotMapped]
    public DateTimeOffset? DeadLetteredAt => ToOffset(this.DeadLetteredAtTicks);

    /// <summary>Gets a value indicating whether this row will never be claimed again.</summary>
    [NotMapped]
    public bool IsTerminal => this.ProcessedAtTicks.HasValue || this.DeadLetteredAtTicks.HasValue;

#pragma warning disable CS8618 // Materialization constructor.
    private OutboxMessage() { }
#pragma warning restore CS8618

    private OutboxMessage(
        Guid id,
        string eventAlias,
        string handlerAlias,
        string payload,
        string? partitionKey,
        long occurredAtTicks,
        long nextAttemptAtTicks) {

        this.Id = id;
        this.EventAlias = eventAlias;
        this.HandlerAlias = handlerAlias;
        this.Payload = payload;
        this.PartitionKey = partitionKey;
        this.OccurredAtTicks = occurredAtTicks;
        this.NextAttemptAtTicks = nextAttemptAtTicks;
    }

    /// <summary>
    /// Creates a pending delivery of <paramref name="eventAlias"/> to <paramref name="handlerAlias"/>,
    /// eligible for claiming immediately.
    /// </summary>
    public static OutboxMessage Pending(
        string eventAlias,
        string handlerAlias,
        string payload,
        string? partitionKey,
        DateTimeOffset occurredAt) {

        Preca.ThrowIfNullOrWhiteSpace(eventAlias);
        Preca.ThrowIfNullOrWhiteSpace(handlerAlias);
        Preca.ThrowIfNull(payload);

        long occurredAtTicks = occurredAt.UtcTicks;

        return new OutboxMessage(
            Guid.CreateVersion7(),
            eventAlias,
            handlerAlias,
            payload,
            partitionKey,
            occurredAtTicks,
            nextAttemptAtTicks: occurredAtTicks);
    }

    /// <summary>
    /// Takes ownership of this row. Only the in-memory claim path calls this; the relational strategies
    /// perform the same assignment inside their claim statement, where it is atomic.
    /// </summary>
    internal void AcquireLock(string workerId, long lockExpiresAtTicks) {
        this.LockId = workerId;
        this.LockExpiresAtTicks = lockExpiresAtTicks;
    }

    /// <summary>Records a successful dispatch and releases the lock.</summary>
    public void MarkProcessed(DateTimeOffset processedAt, string processedBy) {
        this.ProcessedAtTicks = processedAt.UtcTicks;
        this.ProcessedBy = processedBy;
        this.LastError = null;
        ReleaseLock();
    }

    /// <summary>
    /// Records a failed dispatch and schedules the next attempt after an exponential backoff, releasing the
    /// lock so the row is not held for the remainder of its lock duration.
    /// </summary>
    public void ScheduleRetry(DateTimeOffset now, TimeSpan delay, string error) {
        this.Attempts++;
        this.LastError = error;
        this.NextAttemptAtTicks = now.Add(delay).UtcTicks;
        ReleaseLock();
    }

    /// <summary>
    /// Gives up on this row permanently. Unlike an exhausted retry count, this is an explicit terminal state
    /// that can be queried, alerted on and replayed deliberately.
    /// </summary>
    public void MarkDeadLettered(DateTimeOffset now, string error) {
        this.Attempts++;
        this.LastError = error;
        this.DeadLetteredAtTicks = now.UtcTicks;
        ReleaseLock();
    }

    private void ReleaseLock() {
        this.LockId = null;
        this.LockExpiresAtTicks = null;
    }

    private static DateTimeOffset? ToOffset(long? ticks) {
        return ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : null;
    }
}
