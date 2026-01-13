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
public interface IValueObject<out TSelf, in TValue> : IValueObject {
    static abstract TSelf New(TValue value);
}
public interface IValueObject<out TSelf, in TValue1, in TValue2> : IValueObject {
    static abstract TSelf New(TValue1 value1, TValue2 value2);
}
public interface IValueObject<out TSelf, T1, T2, T3> : IValueObject {
    static abstract TSelf New(T1 value1, T2 value2, T3 value3);
}

public interface IValueObject<out TSelf, T1, T2, T3, T4> : IValueObject {
    static abstract TSelf New(T1 value1, T2 value2, T3 value3, T4 value4);
}