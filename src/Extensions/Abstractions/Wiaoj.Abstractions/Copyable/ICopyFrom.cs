namespace Wiaoj.Abstractions;

/// <summary>
/// Defines a contract for an object that can update its own state by copying values from a source object.
/// </summary>
/// <remarks>
/// <para>
/// This interface is typically implemented when you need to refresh an existing instance's properties 
/// from another object (such as a DTO or a template) without creating a new instance.
/// </para>
/// <para>
/// <b>Inverse of</b> <see cref="ICopyTo{T}"/>: here the current instance is the <i>target</i> being
/// updated, whereas in <see cref="ICopyTo{T}"/> the current instance is the <i>source</i>. A type
/// may implement one, both, or neither depending on which direction of mapping it needs to own.
/// </para>
/// <para>
/// <b>Note on <see cref="CopyableExtensions.CopyFrom{T}(T, T)"/>:</b> because that extension shares
/// the exact name and effective call-site signature as this interface's instance method, a normal
/// implicit implementation will shadow it — <c>foo.CopyFrom(bar)</c> resolves to the instance member,
/// not the extension, so the extension's null-guard is bypassed in practice. The extension only adds
/// value for explicit interface implementations or fully generic code constrained to
/// <see cref="ICopyFrom{T}"/>.
/// </para>
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