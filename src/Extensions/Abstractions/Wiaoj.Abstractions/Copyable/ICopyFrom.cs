namespace Wiaoj.Abstractions;
/// <summary>
/// Provides a method to copy state from another instance into this one.
/// </summary>
public interface ICopyFrom<in T> {
    void CopyFrom(T source);
}