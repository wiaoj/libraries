using Tyto;
using Wiaoj.Ddd.DomainEvents;

namespace Wiaoj.Ddd.Tyto;
internal sealed class AutoPublishIntegrationEventHandler<TDomainEvent, TIntegrationEvent>(
    IBus bus,
    IIntegrationEventMapper<TDomainEvent, TIntegrationEvent> mapper)
    : IPostDomainEventHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
    where TIntegrationEvent : class, IEvent {
    public ValueTask Handle(TDomainEvent @event, CancellationToken cancellationToken) {
        TIntegrationEvent integrationMessage = mapper.Map(@event);

        return bus.PublishAsync(integrationMessage, cancellationToken);
    }
}