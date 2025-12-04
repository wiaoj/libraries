namespace Wiaoj.Abstractions;
/// <summary>
/// Supports deep cloning only.
/// </summary>
public interface IDeepCloneable<T> {
    /// <summary>Creates a deep copy of the object.</summary>
    T DeepClone();
}