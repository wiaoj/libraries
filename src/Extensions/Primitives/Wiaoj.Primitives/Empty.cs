using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;   
/// <summary>
/// Represents an empty value — similar to <see cref="void"/> in functional languages.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
[JsonConverter(typeof(EmptyJsonConverter))]
public readonly struct Empty : IEquatable<Empty> {
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

    /// <summary>
    /// Always returns true because all instances of <see cref="Empty"/> are equal.
    /// </summary>
    public override bool Equals(object? obj) {
        return obj is Empty;
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return 0;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return nameof(Empty);
    }

    /// <inheritdoc/>
    public static bool operator ==(Empty left, Empty right) {
        return left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator !=(Empty left, Empty right) {
        return !(left == right);
    }
}