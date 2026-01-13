using Wiaoj.Ddd.DomainEvents;

namespace Wiaoj.Ddd;

public interface IIntegrationEventMapper<in TDomainEvent, out TIntegrationEvent>
    where TDomainEvent : IDomainEvent
    where TIntegrationEvent : class {
    TIntegrationEvent Map(TDomainEvent domainEvent);
}