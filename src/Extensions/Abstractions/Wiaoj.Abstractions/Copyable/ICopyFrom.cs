namespace Wiaoj.Abstractions;
/// <summary>
/// Defines a contract for an object that can update its own state by copying values from a source object.
/// </summary>
/// <remarks>
/// This interface is typically implemented when you need to refresh an existing instance's properties 
/// from another object (such as a DTO or a template) without creating a new instance.
/// </remarks>
/// <typeparam name="T">The type of the source object from which data will be copied.</typeparam>
public interface ICopyFrom<in T> {
    /// <summary>
    /// Copies the state/properties from the specified <paramref name="source"/> into the current instance.
    /// </summary>
    /// <param name="source">The source object containing the values to be copied.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null and the implementation requires it.</exception>
    void CopyFrom(T source);
}