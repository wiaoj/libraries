using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters; 
/// <summary>
/// A custom <see cref="JsonConverter{T}"/> for serializing and deserializing <see cref="Percentage"/> values.
/// Supports reading from both number (0.5) and string ("50%") JSON tokens.
/// </summary>
public sealed class PercentageJsonConverter : JsonConverter<Percentage> {
    /// <summary>
    /// Reads and converts the JSON to type <see cref="Percentage"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is invalid or the value is out of range.</exception>
    public override Percentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Number) {
            double value = reader.GetDouble();
            if(value >= Percentage.Zero && value <= Percentage.Full) {
                return Percentage.FromDouble(value);
            }
            throw new JsonException($"Percentage value {value} is out of range (0.0 - 1.0).");
        }

        if(reader.TokenType == JsonTokenType.String) {
            string? stringValue = reader.GetString();
            if(stringValue is not null) {
                if(Percentage.TryParseInternal(stringValue.AsSpan(), CultureInfo.InvariantCulture, out Percentage result)) {
                    return result;
                }
            }
            throw new JsonException($"Could not parse '{stringValue}' as a Percentage.");
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for Percentage.");
    }

    /// <summary>
    /// Writes a specified value as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options) {
        // Serialize as a raw number (e.g., 0.5) to maintain precision and standard format.
        writer.WriteNumberValue(value.Value);
    }
}