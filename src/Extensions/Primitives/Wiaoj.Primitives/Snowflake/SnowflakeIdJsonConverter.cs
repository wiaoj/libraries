using System.Buffers;
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
        if (reader.TokenType == JsonTokenType.Number) {
            if (reader.TryGetInt64(out long value)) {
                return new SnowflakeId(value);
            }
        }

        // String path: Use UTF-8 span parsing (avoids allocating a string)
        if (reader.TokenType == JsonTokenType.String) {
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
            if (SnowflakeId.TryParse(span, out SnowflakeId result)) {
                return result;
            }
        }

        throw new JsonException("Invalid SnowflakeId format. Expected String (Numeric/Guid) or Number.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SnowflakeId value, JsonSerializerOptions options) {
        Span<byte> buffer = stackalloc byte[20]; 
        if (value.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture)) {
            writer.WriteStringValue(buffer[..written]);
        }
        else {
            // Fallback (unlikely)
            writer.WriteStringValue(value.ToString());
        }
    }

    /// <inheritdoc/>
    public override SnowflakeId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? val = reader.GetString();
        if (string.IsNullOrEmpty(val))
            return SnowflakeId.Empty;
        return SnowflakeId.Parse(val);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, SnowflakeId value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }
}