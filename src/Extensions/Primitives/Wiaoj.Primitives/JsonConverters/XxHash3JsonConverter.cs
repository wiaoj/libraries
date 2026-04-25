using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Primitives.JsonConverters;

/// <summary>
/// Serializes and deserializes <see cref="XxHash3"/> instances to and from their hexadecimal string representations.
/// <para>
/// This converter is optimized for performance, utilizing <see cref="Utf8JsonReader.ValueSpan"/> to avoid
/// unnecessary string allocations when possible. It also provides full support for using <see cref="XxHash3"/>
/// as keys in <see cref="Dictionary{TKey, TValue}"/>.
/// </para>
/// </summary>
public sealed class XxHash3JsonConverter : JsonConverter<XxHash3> {
    /// <summary>
    /// Reads and converts the JSON to type <see cref="XxHash3"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert (always <see cref="XxHash3"/>).</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted <see cref="XxHash3"/> value.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON token is not a string or the hexadecimal format is invalid/incorrect length.
    /// </exception>
    public override XxHash3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) return XxHash3.Empty;

        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected JSON string for XxHash3, but found '{reader.TokenType}'.");

        // Optimization: 8 bytes = 16 hex characters. 
        // We use the span directly if it's a single contiguous segment.
        if(reader.HasValueSequence || reader.ValueSpan.Length != 16) {
            return ParseString(reader.GetString());
        }

        return ParseHexFromSpan(reader.ValueSpan);
    }

    /// <summary>
    /// Writes the <see cref="XxHash3"/> value as a hexadecimal JSON string.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="XxHash3"/> value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, XxHash3 value, JsonSerializerOptions options) {
        // XxHash3.ToString() implementation returns a 16-char hex string.
        writer.WriteStringValue(value.ToString());
    }

    /// <summary>
    /// Reads an <see cref="XxHash3"/> from a JSON property name. 
    /// Enables support for <see cref="XxHash3"/> as a dictionary key.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The parsed <see cref="XxHash3"/> from the property name.</returns>
    public override XxHash3 ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return ParseString(reader.GetString());
    }

    /// <summary>
    /// Writes an <see cref="XxHash3"/> as a JSON property name.
    /// Enables support for <see cref="XxHash3"/> as a dictionary key.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="XxHash3"/> value to use as a property name.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, XxHash3 value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }

    /// <summary>
    /// Converts a string representation of a hex hash into an <see cref="XxHash3"/>.
    /// </summary>
    /// <param name="hex">The hex string to parse.</param>
    /// <returns>A new <see cref="XxHash3"/>.</returns>
    /// <exception cref="JsonException">Thrown if the string is not a valid 16-character hex sequence.</exception>
    private static XxHash3 ParseString(string? hex) {
        if(string.IsNullOrEmpty(hex)) return XxHash3.Empty;

        if(!XxHash3.TryParse(hex, out XxHash3 result)) {
            throw new JsonException($"The value '{hex}' is not a valid 16-character hexadecimal string for XxHash3.");
        }

        return result;
    }

    /// <summary>
    /// Converts a UTF-8 encoded hex span directly into an <see cref="XxHash3"/> without allocating a temporary string.
    /// </summary>
    /// <param name="utf8Hex">The UTF-8 bytes representing the hex string.</param>
    /// <returns>A new <see cref="XxHash3"/>.</returns>
    /// <exception cref="JsonException">Thrown if the hex sequence is invalid.</exception>
    private static XxHash3 ParseHexFromSpan(ReadOnlySpan<byte> utf8Hex) {
        // 16 characters = 16 bytes in ASCII/UTF8. Safe for stackalloc.
        Span<char> charBuffer = stackalloc char[16];
        int written = System.Text.Encoding.ASCII.GetChars(utf8Hex, charBuffer);

        if(!XxHash3.TryParse(charBuffer[..written], out XxHash3 result)) {
            throw new JsonException("Failed to decode XxHash3 from hexadecimal UTF-8 sequence.");
        }

        return result;
    }
}