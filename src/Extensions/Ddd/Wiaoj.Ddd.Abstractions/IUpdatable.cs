using Wiaoj.Ddd.Abstractions.DomainEvents;

namespace Wiaoj.Ddd.Abstractions;

public interface IUpdatable {
    DateTimeOffset? UpdatedAt { get; }
    void SetUpdatedAt(DateTimeOffset updatedAt);
}
public interface IDeletable {
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
    void Delete(DateTimeOffset deletedAt);
    void Restore();
}
public interface ICreatable {
    DateTimeOffset CreatedAt { get; }
    void SetCreatedAt(DateTimeOffset createdAt);
}
public interface IAggregate : ICreatable, IUpdatable, IDeletable, IHasDomainEvent;