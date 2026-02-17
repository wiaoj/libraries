using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// Serializes and deserializes <see cref="HmacSha256Hash"/> instances to and from their hexadecimal string representations.
/// <para>
/// This converter is optimized for performance, utilizing <see cref="Utf8JsonReader.ValueSpan"/> to avoid
/// unnecessary string allocations when possible. It also provides full support for using <see cref="HmacSha256Hash"/>
/// as keys in <see cref="Dictionary{TKey, TValue}"/>.
/// </para>
/// </summary>
public sealed class HmacSha256HashJsonConverter : JsonConverter<HmacSha256Hash> { 
    /// <summary>
    /// Reads and converts the JSON to type <see cref="HmacSha256Hash"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert (always <see cref="HmacSha256Hash"/>).</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted <see cref="HmacSha256Hash"/> value.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON token is not a string or the hexadecimal format is invalid/incorrect length.
    /// </exception>
    public override HmacSha256Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) return HmacSha256Hash.Empty;

        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected JSON string for HmacSha256Hash, but found '{reader.TokenType}'.");

        // Optimization: 32 bytes = 64 hex characters. 
        // We use the span directly if it's a single contiguous segment.
        if(reader.HasValueSequence || reader.ValueSpan.Length != 64) {
            return ParseHex(reader.GetString());
        }

        return ParseHexFromSpan(reader.ValueSpan);
    }

    /// <summary>
    /// Writes the <see cref="HmacSha256Hash"/> value as a hexadecimal JSON string.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="HmacSha256Hash"/> value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, HmacSha256Hash value, JsonSerializerOptions options) {
        // HmacSha256Hash.ToString() implementation returns a 64-char hex string.
        writer.WriteStringValue(value.ToString());
    }

    /// <summary>
    /// Reads a <see cref="HmacSha256Hash"/> from a JSON property name. 
    /// Enables support for <see cref="HmacSha256Hash"/> as a dictionary key.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The parsed <see cref="HmacSha256Hash"/> from the property name.</returns>
    public override HmacSha256Hash ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return ParseHex(reader.GetString());
    }

    /// <summary>
    /// Writes a <see cref="HmacSha256Hash"/> as a JSON property name.
    /// Enables support for <see cref="HmacSha256Hash"/> as a dictionary key.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="HmacSha256Hash"/> value to use as a property name.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, HmacSha256Hash value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }

    /// <summary>
    /// Converts a string representation of a hex hash into a <see cref="HmacSha256Hash"/>.
    /// </summary>
    /// <param name="hex">The hex string to parse.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/>.</returns>
    /// <exception cref="JsonException">Thrown if the string is not a valid 32-byte hex sequence.</exception>
    private static HmacSha256Hash ParseHex(string? hex) {
        if(string.IsNullOrEmpty(hex)) return HmacSha256Hash.Empty;
        try {
            // Convert.FromHexString is optimized in .NET 6+
            return new HmacSha256Hash(Convert.FromHexString(hex));
        }
        catch(Exception ex) {
            throw new JsonException($"The value '{hex}' is not a valid 64-character hexadecimal string for HmacSha256Hash.", ex);
        }
    }

    /// <summary>
    /// Converts a UTF-8 encoded hex span directly into a <see cref="HmacSha256Hash"/> without allocating a temporary string.
    /// </summary>
    /// <param name="utf8Hex">The UTF-8 bytes representing the hex string.</param>
    /// <returns>A new <see cref="HmacSha256Hash"/>.</returns>
    /// <exception cref="JsonException">Thrown if the hex sequence is invalid.</exception>
    private static HmacSha256Hash ParseHexFromSpan(ReadOnlySpan<byte> utf8Hex) {
        // 64 characters = 64 bytes in ASCII/UTF8. Safe for stackalloc.
        Span<char> charBuffer = stackalloc char[64];
        int written = System.Text.Encoding.ASCII.GetChars(utf8Hex, charBuffer);
        try {
            return new HmacSha256Hash(Convert.FromHexString(charBuffer[..written]));
        }
        catch(Exception ex) {
            throw new JsonException("Failed to decode HMAC-SHA256 hash from hexadecimal UTF-8 sequence.", ex);
        }
    }
}