using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Abstractions.DomainEvents;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;

public sealed class DomainEventDispatcherInterceptor(
    IDomainEventDispatcher domainEventDispatcher,
    ISerializer<DddEfCoreOutboxSerializerKey> serializer) : SaveChangesInterceptor {

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
       DbContextEventData eventData,
       InterceptionResult<int> result,
       CancellationToken cancellationToken = default) {

        DbContext? context = eventData.Context;
        if (context is null) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        List<IDomainEvent> domainEvents = GetAndClearDomainEvents(context);
        if (domainEvents.Count == 0) {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        foreach (IDomainEvent domainEvent in domainEvents) {
            await domainEventDispatcher.DispatchPreCommitAsync((dynamic)domainEvent, cancellationToken);
        }

        // Adım 2: Post-commit handler'lar için event'leri Outbox tablosuna yaz.
        List<OutboxMessage> outboxMessages = [.. domainEvents
            .Select(domainEvent => new OutboxMessage(
                Guid.CreateVersion7(),
                domainEvent.GetType().AssemblyQualifiedName!,
                serializer.SerializeToString(domainEvent, domainEvent.GetType()),
                domainEvent.OccurredAt
            ))];

        await context.Set<OutboxMessage>().AddRangeAsync(outboxMessages, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static List<IDomainEvent> GetAndClearDomainEvents(DbContext context) {
        return [.. context.ChangeTracker
            .Entries<IHasDomainEvent>()
            .Select(e => e.Entity)
            .SelectMany(aggregate => {
                List<IDomainEvent> events = [.. aggregate.DomainEvents];
                aggregate.ClearDomainEvents();
                return events;
            })];
    }
}