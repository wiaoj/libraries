using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Ddd.Extensions;
using Wiaoj.Serialization;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;
/// <summary>
/// Intercepts <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> to handle domain events.
/// Dispatches PreCommit events recursively, persists Outbox messages with optimistic ownership, and pushes to the in-memory channel.
/// </summary>
internal sealed class DomainEventDispatcherInterceptor(
    IDomainEventDispatcher domainEventDispatcher,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    OutboxChannel outboxChannel,
    IOptions<OutboxOptions> options,
    OutboxInstanceInfo instanceInfo,
    TimeProvider timeProvider) : SaveChangesInterceptor {

    private readonly int _maxIterations = options.Value.MaxDomainEventDispatchAttempts;
    private List<OutboxMessage>? _messagesToPublish;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
       DbContextEventData eventData,
       InterceptionResult<int> result,
       CancellationToken cancellationToken = default) {

        DbContext? context = eventData.Context;
        if(context is null) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // 1. Recursively process domain events (Pre-Commit)
        List<IDomainEvent> processedEvents = await ProcessDomainEventsRecursivelyAsync(context, cancellationToken);

        // If no events triggered, proceed with normal commit
        if(processedEvents.Count == 0) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // 2. Create Outbox Messages with "Owner-Processing" Strategy (Locked by this instance)
        List<OutboxMessage> outboxMessages = CreateOutboxMessages(processedEvents);

        // 3. Persist messages to the database
        await context.Set<OutboxMessage>().AddRangeAsync(outboxMessages, cancellationToken);

        this._messagesToPublish = outboxMessages;

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default) {

        if(this._messagesToPublish is not null && this._messagesToPublish.Count > 0) {
            await PublishToChannelAsync(this._messagesToPublish, cancellationToken);
            this._messagesToPublish.Clear();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Processes domain events recursively to handle cascading events.
    /// Detects and throws on infinite loops.
    /// </summary>
    private async Task<List<IDomainEvent>> ProcessDomainEventsRecursivelyAsync(DbContext context, CancellationToken cancellationToken) {
        List<IDomainEvent> allProcessedEvents = [];
        int currentIteration = 0;

        while(true) {
            // Fast check before allocating enumerators
            if(!HasDomainEvents(context)) {
                break;
            }

            // Safety Valve: Infinite Loop Detection
            if(currentIteration >= this._maxIterations) {
                string debugInfo = GenerateInfiniteLoopDebugInfo(context, currentIteration);
                throw new InvalidOperationException(debugInfo);
            }

            List<IDomainEvent> batchEvents = GetAndClearDomainEvents(context);

            foreach(IDomainEvent domainEvent in batchEvents) {
                await domainEventDispatcher.DispatchPreCommitCompiledAsync(domainEvent, cancellationToken);
            }

            allProcessedEvents.AddRange(batchEvents);
            currentIteration++;
        }

        return allProcessedEvents;
    }

    /// <summary>
    /// Transforms domain events into OutboxMessage entities.
    /// Applies implicit ownership locking using the current InstanceId.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<OutboxMessage> CreateOutboxMessages(List<IDomainEvent> domainEvents) {
        // Pre-allocate capacity
        List<OutboxMessage> messages = new(domainEvents.Count);

        string? partitionKey = options.Value.PartitionKey;
        string myLockId = instanceInfo.InstanceId;
        DateTimeOffset lockExpiration = timeProvider.GetUtcNow().Add(options.Value.LockDuration);

        foreach(IDomainEvent evt in domainEvents) {
            string content = serializer.SerializeToString(evt, evt.GetType());
            string type = evt.GetType().AssemblyQualifiedName!;

            messages.Add(new OutboxMessage(
                Guid.CreateVersion7(),
                type,
                content,
                evt.OccurredAt,
                partitionKey,
                myLockId,       // Locked by this instance immediately
                lockExpiration  // Expires after configured duration
            ));
        }

        return messages;
    }

    /// <summary>
    /// Pushes messages to the in-memory channel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task PublishToChannelAsync(List<OutboxMessage> messages, CancellationToken cancellationToken) {
        foreach(OutboxMessage message in messages) {
            await outboxChannel.Writer.WriteAsync(message, cancellationToken);
        }
    }

    /// <summary>
    /// Extracts pending domain events from tracked entities and clears them.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<IDomainEvent> GetAndClearDomainEvents(DbContext context) {
        IEnumerable<EntityEntry<IHasDomainEvent>> entries = context.ChangeTracker.Entries<IHasDomainEvent>();
        List<IDomainEvent> events = [];

        foreach(EntityEntry<IHasDomainEvent> entry in entries) {
            IHasDomainEvent entity = entry.Entity;
            if(entity.DomainEvents.Count != 0) {
                events.AddRange(entity.DomainEvents);
                entity.ClearDomainEvents();
            }
        }

        return events;
    }

    /// <summary>
    /// Checks if any entity has pending domain events.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasDomainEvents(DbContext context) {
        return context.ChangeTracker.Entries<IHasDomainEvent>()
            .Any(x => x.Entity.DomainEvents.Count > 0);
    }

    /// <summary>
    /// Generates diagnostic information for infinite loop scenarios.
    /// </summary>
    private static string GenerateInfiniteLoopDebugInfo(DbContext context, int maxIterations) {
        StringBuilder sb = new();
        sb.AppendLine($"Domain event dispatching exceeded the maximum of {maxIterations} iterations. Infinite loop detected.");
        sb.AppendLine("Pending events triggering the overflow:");

        IEnumerable<EntityEntry<IHasDomainEvent>> pending = context.ChangeTracker.Entries<IHasDomainEvent>()
            .Where(e => e.Entity.DomainEvents.Count > 0);

        foreach(EntityEntry<IHasDomainEvent>? entry in pending) {
            string typeName = entry.Entity.GetType().Name;
            sb.AppendLine($" - Aggregate: {typeName}");
            foreach(IDomainEvent evt in entry.Entity.DomainEvents) {
                sb.AppendLine($"    -> Event: {evt.GetType().Name}");
            }
        }

        return sb.ToString();
    }
}