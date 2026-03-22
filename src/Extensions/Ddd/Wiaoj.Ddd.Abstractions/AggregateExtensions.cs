using Wiaoj.Ddd.DomainEvents;

namespace Wiaoj.Ddd;
public static class AggregateExtensions {
    extension<TAggregate>(TAggregate aggregate) where TAggregate : IHasDomainEvent {
        public void RaiseDomainEvent<TDomainEvent>(Func<TAggregate, TDomainEvent> factory) where TDomainEvent : IDomainEvent {
            aggregate.RaiseDomainEvent(factory(aggregate));
        }
    }
}