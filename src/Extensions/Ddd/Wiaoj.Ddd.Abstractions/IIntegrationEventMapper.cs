using Wiaoj.Ddd.Abstractions.DomainEvents;

namespace Wiaoj.Ddd.Abstractions;
public interface IIntegrationEventMapper<in TDomainEvent, out TIntegrationEvent> 
    where TDomainEvent : IDomainEvent 
    where TIntegrationEvent : class {
    TIntegrationEvent Map(TDomainEvent domainEvent);
}