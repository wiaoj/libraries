namespace Wiaoj.Abstractions;
/// <summary>Convenience extensions for copyable objects.</summary>
public static class CopyableExtensions {
    /// <summary>
    /// Copies state from <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    public static void CopyFrom<T>(this T target, T source) where T : ICopyFrom<T> {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(target);
        target.CopyFrom(source);
    }

    /// <summary>
    /// Copies state of <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    public static void CopyTo<T>(this T source, T target) where T : ICopyTo<T> {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(target);
        source.CopyTo(target);
    }

    /// <summary>
    /// Copies state into a fresh deep clone.
    /// Useful if <typeparamref name="T"/> implements both <see cref="ICopyFrom{T}"/> and <see cref="IDeepCloneable{T}"/>.
    /// </summary>
    public static T CopyIntoNew<T>(this T source) where T : ICopyFrom<T>, new() {
        Preca.ThrowIfNull(source);
        T clone = new();
        clone.CopyFrom(source);
        return clone;
    }
}