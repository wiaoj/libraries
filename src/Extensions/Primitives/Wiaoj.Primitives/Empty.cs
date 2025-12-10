using System.Runtime.InteropServices;

namespace Wiaoj.Primitives;   
/// <summary>
/// Represents an empty value — similar to <see cref="void"/> in functional languages.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct Empty : IEquatable<Empty> {
    /// <summary>
    /// The default and only instance of <see cref="Empty"/>.
    /// </summary>
    public static Empty Default => new();

    /// <summary>
    /// Always returns true because all instances of <see cref="Empty"/> are equal.
    /// </summary>
    public bool Equals(Empty other) {
        return true;
    } 

    /// <inheritdoc/>
    public override int GetHashCode() {
        return 0;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return nameof(Empty);
    } 
} 