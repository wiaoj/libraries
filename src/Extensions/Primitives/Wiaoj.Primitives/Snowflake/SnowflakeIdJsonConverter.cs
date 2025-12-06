using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.Snowflake;
/// <summary>
/// Custom JSON converter for <see cref="SnowflakeId"/>.
/// Handles serialization to String (to preserve precision in JS) and deserialization from String/Number/Guid.
/// </summary>
public class SnowflakeIdJsonConverter : JsonConverter<SnowflakeId> {
    /// <inheritdoc/>
    public override SnowflakeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            string? val = reader.GetString();
            if (string.IsNullOrEmpty(val))
                return SnowflakeId.Empty;

            return SnowflakeId.Parse(val, CultureInfo.InvariantCulture);
        }
        else if (reader.TokenType == JsonTokenType.Number) {
            return new SnowflakeId(reader.GetInt64());
        }
        throw new JsonException("Invalid SnowflakeId format. Expected String (Numeric/Guid) or Number.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SnowflakeId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString());
    }

    /// <inheritdoc/>
    public override SnowflakeId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // Key her zaman string gelir
        string? val = reader.GetString();
        if (string.IsNullOrEmpty(val))
            return SnowflakeId.Empty;
        return SnowflakeId.Parse(val, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, SnowflakeId value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }
}