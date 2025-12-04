namespace Wiaoj.Abstractions;
/// <summary>
/// Convenience extension methods for <see cref="IShallowCloneable{T}"/> and <see cref="IDeepCloneable{T}"/>.
/// </summary>
public static class CloneableExtensions {
    /// <summary>
    /// Creates a deep copy of the given instance.
    /// </summary>
    /// <typeparam name="T">The runtime type being cloned.</typeparam>
    /// <param name="source">The instance to clone.</param>
    /// <returns>A deep-cloned instance.</returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public static T DeepClone<T>(this IDeepCloneable<T> source) {
        Preca.ThrowIfNull(source);
        return source.DeepClone();
    }

    /// <summary>
    /// Creates a shallow copy of the given instance.
    /// </summary>
    /// <typeparam name="T">The runtime type being cloned.</typeparam>
    /// <param name="source">The instance to clone.</param>
    /// <returns>A shallow-cloned instance.</returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public static T ShallowClone<T>(this IShallowCloneable<T> source) {
        Preca.ThrowIfNull(source);
        return source.ShallowClone();
    }

    /// <summary>
    /// Clones the object according to the specified <see cref="CloneKind"/>.
    /// Works for types implementing <see cref="ICloneable{T}"/>.
    /// </summary>
    /// <typeparam name="T">The runtime type being cloned.</typeparam>
    /// <param name="source">The instance to clone.</param>
    /// <param name="kind">The cloning strategy to use.</param>
    /// <returns>A cloned instance of type <typeparamref name="T"/>.</returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public static T Clone<T>(this ICloneable<T> source, CloneKind kind = CloneKind.Deep) {
        Preca.ThrowIfNull(source);
        return source.Clone(kind);
    }
}