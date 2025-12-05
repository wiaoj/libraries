namespace Wiaoj.Abstractions;
/// <summary>
/// Specifies how an object should be cloned.
/// </summary>
public enum CloneKind {
    /// <summary>
    /// Creates a shallow copy; reference-type fields/properties
    /// will point to the same instances as the original.
    /// </summary>
    Shallow,

    /// <summary>
    /// Creates a deep copy; nested reference-type fields/properties
    /// are also cloned to produce independent instances.
    /// </summary>
    Deep
}