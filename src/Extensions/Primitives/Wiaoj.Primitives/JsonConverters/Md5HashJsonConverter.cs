using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.JsonConverters; 
/// <summary>
/// Serializes and deserializes <see cref="Md5Hash"/> instances to and from their hexadecimal string representations.
/// <para>
/// This converter is optimized for performance, utilizing <see cref="Utf8JsonReader.ValueSpan"/> to avoid
/// unnecessary string allocations when possible. It also provides full support for using <see cref="Md5Hash"/>
/// as keys in <see cref="Dictionary{TKey, TValue}"/>.
/// </para>
/// </summary>
public sealed class Md5HashJsonConverter : JsonConverter<Md5Hash> {
    /// <summary>
    /// Reads and converts the JSON to type <see cref="Md5Hash"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert (always <see cref="Md5Hash"/>).</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted <see cref="Md5Hash"/> value.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON token is not a string or the hexadecimal format is invalid/incorrect length.
    /// </exception>
    public override Md5Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) return Md5Hash.Empty;

        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected JSON string for Md5Hash, but found '{reader.TokenType}'.");

        // Optimization: 16 bytes = 32 hex characters. 
        // We use the span directly if it's a single contiguous segment.
        if(reader.HasValueSequence || reader.ValueSpan.Length != 32) {
            return ParseString(reader.GetString());
        }

        return ParseHexFromSpan(reader.ValueSpan);
    }

    /// <summary>
    /// Writes the <see cref="Md5Hash"/> value as a hexadecimal JSON string.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="Md5Hash"/> value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Md5Hash value, JsonSerializerOptions options) {
        // Md5Hash.ToString() implementation returns a 32-char hex string.
        writer.WriteStringValue(value.ToString());
    }

    /// <summary>
    /// Reads an <see cref="Md5Hash"/> from a JSON property name. 
    /// Enables support for <see cref="Md5Hash"/> as a dictionary key.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The parsed <see cref="Md5Hash"/> from the property name.</returns>
    public override Md5Hash ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return ParseString(reader.GetString());
    }

    /// <summary>
    /// Writes an <see cref="Md5Hash"/> as a JSON property name.
    /// Enables support for <see cref="Md5Hash"/> as a dictionary key.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="Md5Hash"/> value to use as a property name.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Md5Hash value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }

    /// <summary>
    /// Converts a string representation of a hex hash into an <see cref="Md5Hash"/>.
    /// </summary>
    /// <param name="hex">The hex string to parse.</param>
    /// <returns>A new <see cref="Md5Hash"/>.</returns>
    /// <exception cref="JsonException">Thrown if the string is not a valid 32-character hex sequence.</exception>
    private static Md5Hash ParseString(string? hex) {
        if(string.IsNullOrEmpty(hex)) return Md5Hash.Empty;

        if(!Md5Hash.TryParse(hex, out Md5Hash result)) {
            throw new JsonException($"The value '{hex}' is not a valid 32-character hexadecimal string for Md5Hash.");
        }

        return result;
    }

    /// <summary>
    /// Converts a UTF-8 encoded hex span directly into an <see cref="Md5Hash"/> without allocating a temporary string.
    /// </summary>
    /// <param name="utf8Hex">The UTF-8 bytes representing the hex string.</param>
    /// <returns>A new <see cref="Md5Hash"/>.</returns>
    /// <exception cref="JsonException">Thrown if the hex sequence is invalid.</exception>
    private static Md5Hash ParseHexFromSpan(ReadOnlySpan<byte> utf8Hex) {
        // 32 characters = 32 bytes in ASCII/UTF8. Safe for stackalloc.
        Span<char> charBuffer = stackalloc char[32];
        int written = System.Text.Encoding.ASCII.GetChars(utf8Hex, charBuffer);

        if(!Md5Hash.TryParse(charBuffer[..written], out Md5Hash result)) {
            throw new JsonException("Failed to decode Md5Hash from hexadecimal UTF-8 sequence.");
        }

        return result;
    }
}