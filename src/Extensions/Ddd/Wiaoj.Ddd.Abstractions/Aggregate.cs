using Wiaoj.Ddd.Abstractions.DomainEvents;
using Wiaoj.Ddd.Abstractions.Exceptions;
using Wiaoj.Ddd.Abstractions.ValueObjects;

namespace Wiaoj.Ddd.Abstractions;

public abstract class Aggregate<TId> : Entity<TId>, IAggregate where TId : notnull, IId {
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public bool IsDeleted => this.DeletedAt.HasValue;

    protected readonly List<IDomainEvent> domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => this.domainEvents.AsReadOnly();

    public RowVersion Version { get; set; }

    protected Aggregate() { }
    protected Aggregate(TId id) : base(id) { }

    public void Delete(DateTimeOffset deletedAt) {
        Preca.ThrowIf<EntityAlreadyDeletedException>(this.IsDeleted);
        this.DeletedAt = deletedAt;
    }

    public void Restore() {
        Preca.ThrowIfFalse<EntityNotDeletedException>(this.IsDeleted);
        this.DeletedAt = null;
    }

    public void SetCreatedAt(DateTimeOffset createdAt) {
        Preca.ThrowIf<CreatedAtAlreadySetException>(this.CreatedAt != default);
        this.CreatedAt = createdAt;
    }

    public void SetUpdatedAt(DateTimeOffset updatedAt) {
        Preca.ThrowIfDefault(updatedAt);
        this.UpdatedAt = updatedAt;
    }

    [Obsolete("Removed latest versions")]
    public void RaiseDomainEvent(IDomainEvent @event) {
        Preca.ThrowIfNull(@event);
        this.domainEvents.Add(@event);
    }

    public void RaiseDomainEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent {
        Preca.ThrowIfNull(@event);
        this.domainEvents.Add(@event);
    }

    public void ClearDomainEvents() {
        this.domainEvents.Clear();
    }
}

public static class Ex {
    extension<TAggregate>(TAggregate aggregate) where TAggregate : IHasDomainEvent {
        public void RaiseDomainEvent<TDomainEvent>(Func<TAggregate, TDomainEvent> factory) where TDomainEvent : IDomainEvent {
            aggregate.RaiseDomainEvent(factory(aggregate));
        }
    }
}

//public abstract class Entity<TId> where TId : class, IId {
//    public TId Id { get; private set; }

//#pragma warning disable CS8618
//    protected Entity() { }
//#pragma warning restore CS8618

//    protected Entity(TId id) {
//        this.Id = Preca.Extensions.ThrowIfNull(id);
//    }

//    public override bool Equals(object? obj) {
//        if(obj is not Entity<TId> other)
//            return false;

//        if(ReferenceEquals(this, other))
//            return true;

//        return this.Id != default && other.Id != default && this.Id.Equals(other.Id);
//    }

//    public override int GetHashCode() => HashCode.Combine(GetType(), this.Id);
//}

public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull {
    public TId Id { get; protected set; }

#pragma warning disable CS8618
    protected Entity() { }
#pragma warning restore CS8618

    protected Entity(TId id) {
        if(id.Equals(default(TId))) {
            throw new ArgumentException("Id cannot be default", nameof(id));
        }

        this.Id = id;
    }

    public bool Equals(Entity<TId>? other) {
        if(other is null) {
            return false;
        }

        if(ReferenceEquals(this, other)) {
            return true;
        }

        if(GetType() != other.GetType()) {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(this.Id, other.Id);
    }

    public override bool Equals(object? obj) {
        return Equals(obj as Entity<TId>);
    }

    public override int GetHashCode() {
        return HashCode.Combine(GetType(), this.Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) {
        if(left is null && right is null) {
            return true;
        }

        if(left is null || right is null) {
            return false;
        }

        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) {
        return !(left == right);
    }
}
