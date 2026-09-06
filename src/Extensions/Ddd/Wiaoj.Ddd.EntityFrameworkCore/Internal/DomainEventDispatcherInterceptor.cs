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
/// Intercepts <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> to dispatch pre-commit domain
/// events and enqueue post-commit work into the outbox.
/// </summary>
/// <remarks>
/// <para>
/// Pre-commit handlers run inside the caller's transaction: if they throw, the whole save rolls back with
/// them. Post-commit work is written to the outbox in that same transaction, so it is durable exactly when
/// the state change it describes is durable — no dual write, and nothing published for a transaction that
/// later rolled back.
/// </para>
/// <para>
/// <b>One row per (event, handler).</b> The handler set is resolved here, at enqueue time, and each handler
/// gets its own row. That is what confines a failure to the handler that failed instead of replaying all of
/// them.
/// </para>
/// <para>
/// Registered as a stateless singleton so it works uniformly across scoped, pooled and
/// <c>IDbContextFactory</c> registrations.
/// </para>
/// </remarks>
internal sealed class DomainEventDispatcherInterceptor<TContext>(
    IServiceProvider rootServiceProvider,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    IOutboxAliasRegistry aliases,
    OutboxHandlerCatalog handlerCatalog,
    ILogger<DomainEventDispatcherInterceptor<TContext>> logger) : SaveChangesInterceptor where TContext : DbContext {

    private readonly int _maxIterations = options.Value.MaxDomainEventDispatchAttempts;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
       DbContextEventData eventData,
       InterceptionResult<int> result,
       CancellationToken cancellationToken = default) {

        if(eventData.Context is not { } context) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        await using AsyncServiceScope scope = rootServiceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<DddAmbientUnitOfWork>().Current = context as IUnitOfWork;
        IDomainEventDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        List<IDomainEvent> processedEvents = await ProcessDomainEventsRecursivelyAsync(dispatcher, context, cancellationToken);

        if(processedEvents.Count == 0) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        logger.LogDomainEventsProcessed(processedEvents.Count);

        List<OutboxMessage> outboxMessages = FanOut(scope.ServiceProvider, processedEvents);

        if(outboxMessages.Count > 0) {
            await context.Set<OutboxMessage>().AddRangeAsync(outboxMessages, cancellationToken);
            logger.LogOutboxMessagesPersisted(outboxMessages.Count);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Expands each event into one outbox row per registered post-commit handler.
    /// </summary>
    /// <remarks>
    /// An event with no post-commit handler produces no rows at all — there is nothing durable to do, and
    /// writing a row nobody will act on only creates a queue that never drains.
    /// </remarks>
    private List<OutboxMessage> FanOut(IServiceProvider scopedProvider, List<IDomainEvent> domainEvents) {
        List<OutboxMessage> messages = [];
        string? partitionKey = options.Value.PartitionKey;

        foreach(IDomainEvent domainEvent in domainEvents) {
            Type eventType = domainEvent.GetType();
            string[] handlerAliases = handlerCatalog.GetHandlerAliases(scopedProvider, eventType);

            if(handlerAliases.Length == 0) {
                continue;
            }

            aliases.Register(eventType);

            string eventAlias = aliases.GetEventAlias(eventType);
            string payload = serializer.SerializeToString(domainEvent, eventType);

            foreach(string handlerAlias in handlerAliases) {
                messages.Add(OutboxMessage.Pending(eventAlias, handlerAlias, payload, partitionKey, domainEvent.OccurredAt));
            }
        }

        return messages;
    }

    /// <summary>
    /// Processes domain events recursively to handle cascading events, throwing when they do not settle.
    /// </summary>
    private async Task<List<IDomainEvent>> ProcessDomainEventsRecursivelyAsync(
        IDomainEventDispatcher dispatcher,
        DbContext context,
        CancellationToken cancellationToken) {
        List<IDomainEvent> allProcessedEvents = [];
        int currentIteration = 0;

        while(true) {
            if(!HasDomainEvents(context)) {
                break;
            }

            if(currentIteration >= this._maxIterations) {
                throw new InvalidOperationException(GenerateInfiniteLoopDebugInfo(context, currentIteration));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasDomainEvents(DbContext context) {
        return context.ChangeTracker.Entries<IHasDomainEvent>()
            .Any(x => x.Entity.DomainEvents.Count > 0);
    }

    private static string GenerateInfiniteLoopDebugInfo(DbContext context, int maxIterations) {
        StringBuilder sb = new();
        sb.AppendLine($"Domain event dispatching exceeded the maximum of {maxIterations} iterations. Infinite loop detected.");
        sb.AppendLine("Pending events triggering the overflow:");

        IEnumerable<EntityEntry<IHasDomainEvent>> pending = context.ChangeTracker.Entries<IHasDomainEvent>()
            .Where(e => e.Entity.DomainEvents.Count > 0);

        foreach(EntityEntry<IHasDomainEvent>? entry in pending) {
            sb.AppendLine($" - Aggregate: {entry.Entity.GetType().Name}");
            foreach(IDomainEvent evt in entry.Entity.DomainEvents) {
                sb.AppendLine($"    -> Event: {evt.GetType().Name}");
            }
        }

        return sb.ToString();
    }
}
