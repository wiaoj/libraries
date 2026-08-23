namespace Wiaoj.Abstractions;

/// <summary>
/// Defines a contract for an object that can project or copy its state into a target object.
/// </summary>
/// <remarks>
/// <para>
/// This interface is useful for mapping operations where the current object is responsible 
/// for populating the fields of a target instance (e.g., mapping a Domain Model to an existing DTO).
/// </para>
/// <para>
/// <b>Inverse of</b> <see cref="ICopyFrom{T}"/>: here the current instance is the <i>source</i> pushing
/// data out, whereas in <see cref="ICopyFrom{T}"/> the current instance is the <i>target</i> pulling
/// data in. Prefer implementing whichever direction matches which side owns the mapping logic —
/// typically the richer/domain type implements <see cref="ICopyTo{T}"/> to project into a thinner DTO,
/// and the thinner type implements <see cref="ICopyFrom{T}"/> to refresh itself from the domain type.
/// </para>
/// <para>
/// Same shadowing caveat as <see cref="ICopyFrom{T}"/> applies to
/// <see cref="CopyableExtensions.CopyTo{T}(T, T)"/> — a normal implicit implementation
/// shadows the extension, so the extension is only reachable via explicit interface
/// implementation or generic code constrained to <see cref="ICopyTo{T}"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the target object that will receive the data.</typeparam>
public interface ICopyTo<in T> {
    /// <summary>
    /// Copies the state/properties of the current instance into the specified <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The destination object that will be updated with the current instance's values.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> is null.</exception>
    void CopyTo(T target);
}