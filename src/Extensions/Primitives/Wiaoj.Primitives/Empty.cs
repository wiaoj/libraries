using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;   
/// <summary>
/// Represents an empty value — similar to <see cref="void"/> in functional languages.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
[JsonConverter(typeof(EmptyJsonConverter))]
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

/// <summary>
/// Empty Json Converter
/// </summary>
public sealed class EmptyJsonConverter : JsonConverter<Empty> {
    /// <inheritdoc/>
    public override Empty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // (null, {}, string)
        reader.Skip();
        return Empty.Default;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Empty value, JsonSerializerOptions options) {
        // "{}"
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}