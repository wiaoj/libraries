using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// High-performance, zero-allocation JSON converter for <see cref="CursorToken"/>.
/// </summary>
public sealed class CursorTokenJsonConverter : JsonConverter<CursorToken> {
    /// <inheritdoc/>
    public override CursorToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return CursorToken.Empty;
        }

        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException("Expected string token for CursorToken.");
        }

        if(!reader.HasValueSequence) {
            return CursorToken.Parse(reader.ValueSpan);
        }

        ReadOnlySequence<byte> sequence = reader.ValueSequence;
        Span<byte> stackBuffer = stackalloc byte[(int)sequence.Length];
        sequence.CopyTo(stackBuffer);

        return CursorToken.Parse(stackBuffer);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CursorToken value, JsonSerializerOptions options) {
        if(value.IsEmpty) {
            writer.WriteStringValue(string.Empty);
            return;
        }

        writer.WriteStringValue(value.Value.AsSpan());
    }
}