using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Cryptography.Hashing;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// Serializes and deserializes <see cref="Sha512Hash"/> instances to and from their hexadecimal string representations.
/// <para>
/// This converter is optimized for performance, utilizing <see cref="Utf8JsonReader.ValueSpan"/> to avoid
/// unnecessary string allocations when possible. It also provides full support for using <see cref="Sha512Hash"/>
/// as keys in <see cref="Dictionary{TKey, TValue}"/>.
/// </para>
/// </summary>
public sealed class Sha512HashJsonConverter : JsonConverter<Sha512Hash> {
    /// <summary>
    /// Reads and converts the JSON to type <see cref="Sha512Hash"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert (always <see cref="Sha512Hash"/>).</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted <see cref="Sha512Hash"/> value.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON token is not a string or the hexadecimal format is invalid/incorrect length.
    /// </exception>
    public override Sha512Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) return Sha512Hash.Empty;

        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected JSON string for Sha512Hash, but found '{reader.TokenType}'.");

        // Optimization: 64 bytes = 128 hex characters. 
        // We use the span directly if it's a single contiguous segment.
        if(reader.HasValueSequence || reader.ValueSpan.Length != 128) {
            return ParseHex(reader.GetString());
        }

        return ParseHexFromSpan(reader.ValueSpan);
    }

    /// <summary>
    /// Writes the <see cref="Sha512Hash"/> value as a hexadecimal JSON string.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="Sha512Hash"/> value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Sha512Hash value, JsonSerializerOptions options) {
        // Sha512Hash.ToString() implementation returns a 128-char hex string.
        writer.WriteStringValue(value.ToString());
    }

    /// <summary>
    /// Reads a <see cref="Sha512Hash"/> from a JSON property name. 
    /// Enables support for <see cref="Sha512Hash"/> as a dictionary key.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The parsed <see cref="Sha512Hash"/> from the property name.</returns>
    public override Sha512Hash ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return ParseHex(reader.GetString());
    }

    /// <summary>
    /// Writes a <see cref="Sha512Hash"/> as a JSON property name.
    /// Enables support for <see cref="Sha512Hash"/> as a dictionary key.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
    /// <param name="value">The <see cref="Sha512Hash"/> value to use as a property name.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Sha512Hash value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }

    /// <summary>
    /// Converts a string representation of a hex hash into a <see cref="Sha512Hash"/>.
    /// </summary>
    /// <param name="hex">The hex string to parse.</param>
    /// <returns>A new <see cref="Sha512Hash"/>.</returns>
    /// <exception cref="JsonException">Thrown if the string is not a valid 64-byte hex sequence.</exception>
    private static Sha512Hash ParseHex(string? hex) {
        if(string.IsNullOrEmpty(hex)) return Sha512Hash.Empty;
        try {
            // Convert.FromHexString is optimized in .NET 6+
            return new Sha512Hash(Convert.FromHexString(hex));
        }
        catch(Exception ex) {
            throw new JsonException($"The value '{hex}' is not a valid 128-character hexadecimal string for Sha512Hash.", ex);
        }
    }

    /// <summary>
    /// Converts a UTF-8 encoded hex span directly into a <see cref="Sha512Hash"/> without allocating a temporary string.
    /// </summary>
    /// <param name="utf8Hex">The UTF-8 bytes representing the hex string.</param>
    /// <returns>A new <see cref="Sha512Hash"/>.</returns>
    /// <exception cref="JsonException">Thrown if the hex sequence is invalid.</exception>
    private static Sha512Hash ParseHexFromSpan(ReadOnlySpan<byte> utf8Hex) {
        // 128 characters = 128 bytes in ASCII/UTF8. Safe for stackalloc.
        Span<char> charBuffer = stackalloc char[128];
        int written = System.Text.Encoding.ASCII.GetChars(utf8Hex, charBuffer);
        try {
            return new Sha512Hash(Convert.FromHexString(charBuffer[..written]));
        }
        catch(Exception ex) {
            throw new JsonException("Failed to decode SHA512 hash from hexadecimal UTF-8 sequence.", ex);
        }
    }
}