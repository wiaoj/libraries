using System.Diagnostics.CodeAnalysis;
using Wiaoj.Ddd.Exceptions;
using Wiaoj.Ddd.ValueObjects;

namespace Wiaoj.Ddd.DomainEvents; 
public abstract record DomainEvent : IDomainEvent {
    public Guid Id { get; }
    public DateTimeOffset OccurredAt { get; }

    protected DomainEvent() : this(TimeProvider.System.GetUtcNow()) { }
    protected DomainEvent(DateTimeOffset occurredAt) {
        Id = Guid.CreateVersion7(occurredAt);
        OccurredAt = occurredAt;
    }
}


public interface IDomainEvent {
    Guid Id { get; }
    DateTimeOffset OccurredAt { get; }
}
public interface IHasDomainEvent {
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void RaiseDomainEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent;
    void ClearDomainEvents();
} 