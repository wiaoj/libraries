using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Internal.Loggers;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Ddd.Extensions;
using Wiaoj.Serialization;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;
/// <summary>
/// Intercepts <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> to handle domain events.
/// Dispatches PreCommit events recursively, persists Outbox messages with optimistic ownership, and pushes to the in-memory channel.
/// <para>
/// Registered as a stateless singleton so it works uniformly across scoped, pooled, and <c>IDbContextFactory</c>
/// DbContext registrations. Per-save state is keyed by the live <see cref="DbContext"/> instance instead of being
/// stored on the interceptor, and scoped handler dependencies are obtained from a dedicated scope whose
/// <see cref="DddAmbientUnitOfWork"/> is seeded with the active context.
/// </para>
/// </summary>
internal sealed class DomainEventDispatcherInterceptor<TContext>(
    IServiceProvider rootServiceProvider,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    OutboxChannel<TContext> outboxChannel,
    IOptions<OutboxOptions> options,
    OutboxInstanceInfo instanceInfo,
    TimeProvider timeProvider,
    ILogger<DomainEventDispatcherInterceptor<TContext>> logger) : SaveChangesInterceptor where TContext : DbContext {

    private readonly int _maxIterations = options.Value.MaxDomainEventDispatchAttempts;

    // Keyed by the live context so a single shared interceptor instance stays stateless and thread-safe.
    // Entries are GC-collected automatically once the context is disposed.
    private static readonly ConditionalWeakTable<DbContext, List<OutboxMessage>> _pendingMessages = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
       DbContextEventData eventData,
       InterceptionResult<int> result,
       CancellationToken cancellationToken = default) {

        // This interceptor is attached only to its own TContext, so a null check is sufficient.
        if(eventData.Context is not { } context) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // A dedicated scope provides scoped handler dependencies. The live context is surfaced through the
        // ambient UnitOfWork so pre-commit handlers participate in the same transaction.
        await using AsyncServiceScope scope = rootServiceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<DddAmbientUnitOfWork>().Current = context as IUnitOfWork;
        IDomainEventDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        // 1. Recursively process domain events (Pre-Commit)
        List<IDomainEvent> processedEvents = await ProcessDomainEventsRecursivelyAsync(dispatcher, context, cancellationToken);

        // If no events triggered, proceed with normal commit
        if(processedEvents.Count == 0) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        logger.LogDomainEventsProcessed(processedEvents.Count);

        // 2. Create Outbox Messages with "Owner-Processing" Strategy (Locked by this instance)
        List<OutboxMessage> outboxMessages = CreateOutboxMessages(processedEvents);

        // 3. Persist messages to the database (same transaction as the aggregate changes)
        await context.Set<OutboxMessage>().AddRangeAsync(outboxMessages, cancellationToken);

        // 4. Stage for fast-path publishing once the transaction commits successfully
        _pendingMessages.AddOrUpdate(context, outboxMessages);

        logger.LogOutboxMessagesPersisted(outboxMessages.Count);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default) {

        if(eventData.Context is { } context
            && _pendingMessages.TryGetValue(context, out List<OutboxMessage>? messages)) {
            _pendingMessages.Remove(context);

            if(messages.Count > 0) {
                await PublishToChannelAsync(messages, cancellationToken);
                logger.LogOutboxMessagesPublishedToChannel(messages.Count);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) {
        // Commit failed: drop staged messages so they are never published; the DB rows were rolled back too.
        if(eventData.Context is { } context) {
            _pendingMessages.Remove(context);
        }

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) {
        if(eventData.Context is { } context) {
            _pendingMessages.Remove(context);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// Processes domain events recursively to handle cascading events.
    /// Detects and throws on infinite loops.
    /// </summary>
    private async Task<List<IDomainEvent>> ProcessDomainEventsRecursivelyAsync(
        IDomainEventDispatcher dispatcher,
        DbContext context,
        CancellationToken cancellationToken) {
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
                await dispatcher.DispatchPreCommitCompiledAsync(domainEvent, cancellationToken);
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
