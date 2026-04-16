using Wiaoj.Ddd.DomainEvents;

namespace Wiaoj.Ddd;

public interface IIntegrationEventMapper<in TDomainEvent, out TIntegrationEvent> : IIntegrationEventMapper
    where TDomainEvent : IDomainEvent
    where TIntegrationEvent : class {
    TIntegrationEvent Map(TDomainEvent domainEvent);
}

public interface IIntegrationEventMapper;