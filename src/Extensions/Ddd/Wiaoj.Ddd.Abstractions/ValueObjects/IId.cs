namespace Wiaoj.Ddd.ValueObjects;

public interface IId;
public interface IId<out TSelf, TValue> : IId, IValueObject<TSelf> {
    TValue Value { get; }
    static abstract TSelf From(TValue value);
}

public interface IId<TSelf> : IId<TSelf, string>;

public interface IValueObject;

public interface IValueObject<out TSelf> : IValueObject {
    static abstract TSelf New();
}
