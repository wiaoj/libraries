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

    /// <summary>
    /// Creates a new instance using the provided factory and copies the state of
    /// the source object into it. This overload is useful when a parameterless
    /// factory is sufficient.
    /// </summary>
    /// <typeparam name="T">
    /// The type being copied. Must implement <see cref="ICopyFrom{T}"/>.
    /// </typeparam>
    /// <param name="source">The source instance whose state will be copied.</param>
    /// <param name="factory">
    /// A factory function that creates a fresh instance of <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// A new instance created by <paramref name="factory"/>, with its state copied
    /// from <paramref name="source"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> or <paramref name="factory"/> is <c>null</c>.
    /// </exception>
    public static T CopyIntoNew<T>(this T source, Func<T> factory) where T : ICopyFrom<T> {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(factory);

        T clone = factory();
        clone.CopyFrom(source);
        return clone;
    }

    /// <summary>
    /// Creates a new instance using the provided factory, which receives the source
    /// object as input, and copies the state of the source object into it.
    /// </summary>
    /// <typeparam name="T">
    /// The type being copied. Must implement <see cref="ICopyFrom{T}"/>.
    /// </typeparam>
    /// <param name="source">The source instance whose state will be copied.</param>
    /// <param name="factory">
    /// A factory function that receives the source instance and returns a new
    /// instance of <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// A new instance created by <paramref name="factory"/>, with its state copied
    /// from <paramref name="source"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> or <paramref name="factory"/> is <c>null</c>.
    /// </exception>
    public static T CopyIntoNew<T>(this T source, Func<T, T> factory) where T : ICopyFrom<T> {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(factory);

        T clone = factory(source);
        clone.CopyFrom(source);
        return clone;
    }

    /// <summary>
    /// Creates a new instance using the provided factory, which receives an external
    /// argument, and copies the state of the source object into it.
    /// </summary>
    /// <typeparam name="T">
    /// The type being copied. Must implement <see cref="ICopyFrom{T}"/>.
    /// </typeparam>
    /// <typeparam name="TArg">
    /// The type of the external argument passed to the factory.
    /// </typeparam>
    /// <param name="source">The source instance whose state will be copied.</param>
    /// <param name="factory">
    /// A factory function that takes an argument of type <typeparamref name="TArg"/>
    /// and returns a new instance of <typeparamref name="T"/>.
    /// </param>
    /// <param name="arg">The argument passed to the factory function.</param>
    /// <returns>
    /// A new instance created by <paramref name="factory"/> using <paramref name="arg"/>,
    /// with its state copied from <paramref name="source"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> or <paramref name="factory"/> is <c>null</c>.
    /// </exception>
    public static T CopyIntoNew<T, TArg>(this T source, Func<TArg, T> factory, TArg arg) where T : ICopyFrom<T> {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(factory);

        T clone = factory(arg);
        clone.CopyFrom(source);
        return clone;
    }

    /// <summary>
    /// Creates a new instance using the provided factory, which receives both the
    /// source object and an external argument, and copies the state of the source
    /// object into it. This is the most flexible overload.
    /// </summary>
    /// <typeparam name="T">
    /// The type being copied. Must implement <see cref="ICopyFrom{T}"/>.
    /// </typeparam>
    /// <typeparam name="TArg">
    /// The type of the external argument passed to the factory.
    /// </typeparam>
    /// <param name="source">The source instance whose state will be copied.</param>
    /// <param name="factory">
    /// A factory function that receives the source instance and an external
    /// argument, and returns a new instance of <typeparamref name="T"/>.
    /// </param>
    /// <param name="arg">The argument passed to the factory function.</param>
    /// <returns>
    /// A new instance created by <paramref name="factory"/> using both
    /// <paramref name="source"/> and <paramref name="arg"/>, with its state
    /// copied from <paramref name="source"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentNullException">
    /// Thrown if <paramref name="source"/> or <paramref name="factory"/> is <c>null</c>.
    /// </exception>
    public static T CopyIntoNew<T, TArg>(this T source, Func<T, TArg, T> factory, TArg arg)
        where T : ICopyFrom<T> {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(factory);

        T clone = factory(source, arg);
        clone.CopyFrom(source);
        return clone;
    }
}