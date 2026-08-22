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

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options) {
        writer.WriteNumberValue(value.Value);
    }

    /// <inheritdoc/>
    public override Percentage ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? propName = reader.GetString();
        if(propName is not null && Percentage.TryParseInternal(propName.AsSpan(), CultureInfo.InvariantCulture, out Percentage result)) {
            return result;
        }
        throw new JsonException($"Invalid property name format for Percentage: '{propName}'.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value.ToString("G", CultureInfo.InvariantCulture));
    }
}