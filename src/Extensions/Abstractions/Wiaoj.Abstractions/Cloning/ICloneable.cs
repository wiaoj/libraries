namespace Wiaoj.Abstractions;
/// <summary>
/// Supports both shallow and deep cloning.
/// </summary>
public interface ICloneable<T> : IShallowCloneable<T>, IDeepCloneable<T> {
    /// <summary>
    /// Clones the object according to the specified strategy.
    /// </summary>
    T Clone(CloneKind kind = CloneKind.Deep)
        => kind == CloneKind.Deep ? DeepClone() : ShallowClone();
}