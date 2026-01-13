using System.Diagnostics.CodeAnalysis;
using Wiaoj.Ddd.Exceptions;
using Wiaoj.Ddd.ValueObjects;

namespace Wiaoj.Ddd.DomainEvents;
public abstract record DomainEvent : IDomainEvent {
    public DomainEventId Id { get; }
    public DateTimeOffset OccurredAt { get; }
    public DomainEventVersion Version { get; }

    protected DomainEvent() : this(TimeProvider.System.GetUtcNow(), DomainEventVersion.New(1)) { }

    protected DomainEvent(DateTimeOffset occurredAt, DomainEventVersion version) {
        this.Id = DomainEventId.New(occurredAt);
        this.OccurredAt = occurredAt;
        this.Version = version;
    }

    public override int GetHashCode() {
        return HashCode.Combine(this.Id, this.OccurredAt, this.Version);
    }
}
public sealed record DomainEventId : IValueObject<DomainEventId, DateTimeOffset>, ITryParsable<DomainEventId, string> {
    public string Value { get; }

#pragma warning disable CS8618 
    private DomainEventId() { }
#pragma warning restore CS8618 
    private DomainEventId(Guid id) {
        this.Value = id.ToString();
    }

    public static DomainEventId New(DateTimeOffset value) {
        return new(Guid.CreateVersion7(value));
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out DomainEventId? domainEventId) {
        domainEventId = null;

        if(Guid.TryParse(value, out Guid parsedGuid)) {
            domainEventId = new DomainEventId(parsedGuid);
            return true;
        }

        return false;
    }

    public static implicit operator string(DomainEventId id) {
        return id.Value;
    }
}

public sealed record DomainEventVersion : IValueObject<DomainEventVersion, int> {
    public int Value { get; }

    private DomainEventVersion() { }
    private DomainEventVersion(int value) {
        this.Value = value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="DomainEventVersionCannotBeNegativeException">Thrown when the value is negative.</exception>
    public static DomainEventVersion New(int value) {
        Preca.ThrowIfNegative<int, DomainEventVersionCannotBeNegativeException>(value);
        return new(value);
    }

    public static implicit operator int(DomainEventVersion version) {
        return version.Value;
    }
}
public sealed class DomainEventVersionCannotBeNegativeException() : DomainException($"Domain event version cannot be negative.");

public interface IDomainEvent {
    DomainEventId Id { get; }
    DateTimeOffset OccurredAt { get; }
    DomainEventVersion Version { get; }
}
public interface IHasDomainEvent {
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void RaiseDomainEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent;
    void ClearDomainEvents();
}

public interface ITryParsable<TSelf, TPrimitive> {
    static abstract bool TryParse(TPrimitive? value, [NotNullWhen(true)] out TSelf? result);
}