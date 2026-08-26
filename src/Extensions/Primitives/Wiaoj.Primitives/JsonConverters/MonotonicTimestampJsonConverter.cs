using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;

/// <summary>
/// Serializes and deserializes <see cref="MonotonicTimestamp"/> to and from JSON.
/// Supports high-performance numeric representation as well as string compatibility.
/// </summary>
public sealed class MonotonicTimestampJsonConverter : JsonConverter<MonotonicTimestamp> {
    /// <inheritdoc/>
    public override MonotonicTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Number) {
            if(reader.TryGetInt64(out long ticks)) {
                return MonotonicTimestamp.FromRawTicks(ticks);
            }
        }

        if(reader.TokenType == JsonTokenType.String) {
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            if(Utf8Parser.TryParse(span, out long ticks, out int bytesConsumed) && bytesConsumed == span.Length) {
                return MonotonicTimestamp.FromRawTicks(ticks);
            }
        }

        throw new JsonException($"Unable to convert JSON token {reader.TokenType} to MonotonicTimestamp. Expected a number or an integer string.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MonotonicTimestamp value, JsonSerializerOptions options) {
        writer.WriteNumberValue(value.RawTicks);
    }

    /// <inheritdoc/>
    public override MonotonicTimestamp ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

        if(Utf8Parser.TryParse(span, out long ticks, out int bytesConsumed) && bytesConsumed == span.Length) {
            return MonotonicTimestamp.FromRawTicks(ticks);
        }

        throw new JsonException("Invalid property name format for MonotonicTimestamp. Expected an integer string.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, MonotonicTimestamp value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.RawTicks.ToString(CultureInfo.InvariantCulture));
    }
}