using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters; 
/// <summary>
/// Serializes and deserializes <see cref="Urn"/> instances to and from JSON strings.
/// <para>
/// This converter also supports using <see cref="Urn"/> as a key in <see cref="Dictionary{TKey, TValue}"/>.
/// </para>
/// </summary>
public sealed class UrnJsonConverter : JsonConverter<Urn> {

    /// <summary>
    /// Reads and converts the JSON to type <see cref="Urn"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="JsonException">Thrown when the JSON value is not a string or the format is invalid.</exception>
    public override Urn Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return Urn.Empty;
        }

        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected JSON String for Urn but got '{reader.TokenType}'.");
        }

        string? value = reader.GetString();

        if(string.IsNullOrEmpty(value)) {
            return Urn.Empty;
        }

        try {
            return Urn.Parse(value);
        }
        catch(FormatException ex) {
            // Wrap the internal format exception into a JsonException for better context during deserialization
            throw new JsonException($"Invalid format for Urn: '{value}'.", ex);
        }
    }

    /// <summary>
    /// Writes a specified <see cref="Urn"/> value as a JSON string.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Urn value, JsonSerializerOptions options) {
        // Value property handles nulls internally ensuring we write a valid string or empty.
        writer.WriteStringValue(value.Value);
    }

    /// <summary>
    /// Reads a dictionary key from a JSON property name.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted key value.</returns>
    public override Urn ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // Ensure the token is a property name or string
        if(reader.TokenType != JsonTokenType.PropertyName && reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected PropertyName for Urn key but got '{reader.TokenType}'.");
        }

        string? value = reader.GetString();

        if(string.IsNullOrEmpty(value)) {
            return Urn.Empty;
        }

        try {
            return Urn.Parse(value);
        }
        catch(FormatException ex) {
            throw new JsonException($"Invalid format for Urn key: '{value}'.", ex);
        }
    }

    /// <summary>
    /// Writes a dictionary key as a JSON property name.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Urn value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}