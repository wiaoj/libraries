using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// High-performance, zero-allocation JSON converter for <see cref="SignedCursorToken"/>.
/// </summary>
public sealed class SignedCursorTokenJsonConverter : JsonConverter<SignedCursorToken> {
    /// <inheritdoc/>
    public override SignedCursorToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return SignedCursorToken.Empty;
        }

        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException("Expected string token for SignedCursorToken.");
        }

        if(!reader.HasValueSequence) {
            return SignedCursorToken.Parse(reader.ValueSpan);
        }

        ReadOnlySequence<byte> sequence = reader.ValueSequence;
        Span<byte> stackBuffer = stackalloc byte[(int)sequence.Length];
        sequence.CopyTo(stackBuffer);

        return SignedCursorToken.Parse(stackBuffer);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SignedCursorToken value, JsonSerializerOptions options) {
        if(value.IsEmpty) {
            writer.WriteStringValue(string.Empty);
            return;
        }

        Span<byte> utf8Buffer = stackalloc byte[128];
        if(value.TryFormat(utf8Buffer, out int bytesWritten)) {
            writer.WriteStringValue(utf8Buffer[..bytesWritten]);
        }
        else {
            writer.WriteStringValue(value.ToString());
        }
    }
}