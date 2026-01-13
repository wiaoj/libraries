using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// Handles strict and flexible serialization of <see cref="UnixTimestamp"/>.
/// Supports both numeric JSON values (standard) and string JSON values (compatibility).
/// Interprets values as MILLISECONDS.
/// </summary>
public sealed class UnixTimestampJsonConverter : JsonConverter<UnixTimestamp> {

    /// <inheritdoc/>
    public override UnixTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // 1. Optimization: Try reading as a number first (most common and fastest case)
        if(reader.TokenType == JsonTokenType.Number) {
            if(reader.TryGetInt64(out long milliseconds)) {
                return UnixTimestamp.From(milliseconds);
            }
        }

        // 2. Fallback: Try reading as a string (common in JS/Web APIs to avoid precision issues)
        if(reader.TokenType == JsonTokenType.String) {
            // Read directly from the raw byte span to avoid allocating a C# string
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            // Direct usage of Utf8Parser is faster than casting to IUtf8SpanParsable
            if(Utf8Parser.TryParse(span, out long milliseconds, out int bytesConsumed) && bytesConsumed == span.Length) {
                return UnixTimestamp.From(milliseconds);
            }
        }

        throw new JsonException($"Unable to convert JSON token {reader.TokenType} to UnixTimestamp. Expected a number or a string containing an integer.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, UnixTimestamp value, JsonSerializerOptions options) {
        // Standard practice is writing timestamps as numbers (integers).
        writer.WriteNumberValue(value.Milliseconds);
    }

    /// <inheritdoc/>
    public override UnixTimestamp ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // Dictionary keys in JSON are always strings.
        // We parse the UTF-8 bytes of the property name directly.
        ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

        if(Utf8Parser.TryParse(span, out long milliseconds, out int bytesConsumed) && bytesConsumed == span.Length) {
            return UnixTimestamp.From(milliseconds);
        }

        throw new JsonException($"Invalid property name format for UnixTimestamp. Expected an integer string.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, UnixTimestamp value, JsonSerializerOptions options) {
        // Property keys in JSON must always be strings.
        // We format the long to string.
        writer.WritePropertyName(value.ToString());
    }
}