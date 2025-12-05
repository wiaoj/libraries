namespace Wiaoj.Abstractions;
/// <summary>
/// Supports shallow cloning only.
/// </summary>
public interface IShallowCloneable<T> {
    /// <summary>Creates a shallow copy of the object.</summary>
    T ShallowClone();
}