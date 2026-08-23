namespace Wiaoj.Abstractions;

/// <summary>
/// Supports both shallow and deep cloning, exposing a single unified entry point
/// that defaults to a deep copy when no strategy is specified.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Clone(CloneKind)"/> is a default interface method (DIM). Types that implicitly
/// implement <see cref="ICloneable{T}"/> do <b>not</b> expose it as an instance member on the
/// concrete type — <c>foo.Clone()</c> will not compile unless the implementer explicitly
/// re-declares it. It is reachable via an <see cref="ICloneable{T}"/>-typed reference,
/// or through <see cref="CloneableExtensions.Clone{T}(ICloneable{T}, CloneKind)"/>, which
/// exists specifically to restore that call-site ergonomics.
/// </para>
/// <para>
/// Prefer calling <see cref="IDeepCloneable{T}.DeepClone"/> or <see cref="IShallowCloneable{T}.ShallowClone"/>
/// directly when the strategy is known at the call site. <see cref="Clone(CloneKind)"/> exists
/// for generic/polymorphic contexts where the strategy is only known as a runtime value
/// (e.g. driven by configuration or a caller-supplied <see cref="CloneKind"/> parameter).
/// </para>
/// </remarks>
/// <typeparam name="T">The type being cloned.</typeparam>
public interface ICloneable<T> : IShallowCloneable<T>, IDeepCloneable<T> {
    /// <summary>
    /// Clones the object using the specified <see cref="CloneKind"/> strategy.
    /// </summary>
    /// <param name="kind">
    /// The cloning strategy to apply. Defaults to <see cref="CloneKind.Deep"/> so that
    /// generic code cloning an unknown <typeparamref name="T"/> gets the safer,
    /// fully-independent copy unless a shallow copy is explicitly requested.
    /// </param>
    /// <returns>A new instance of <typeparamref name="T"/> produced according to <paramref name="kind"/>.</returns>
    T Clone(CloneKind kind = CloneKind.Deep) {
        return kind == CloneKind.Deep ? DeepClone() : ShallowClone();
    }
}