namespace Wiaoj.Abstractions;
/// <summary>
/// Provides a method to copy this instance’s state into another target instance.
/// </summary>
public interface ICopyTo<in T> {
    void CopyTo(T target);
}