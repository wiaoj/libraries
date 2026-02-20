namespace Wiaoj.Abstractions;
/// <summary>
/// Defines a contract for an object that can project or copy its state into a target object.
/// </summary>
/// <remarks>
/// This interface is useful for mapping operations where the current object is responsible 
/// for populating the fields of a target instance (e.g., mapping a Domain Model to an existing DTO).
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