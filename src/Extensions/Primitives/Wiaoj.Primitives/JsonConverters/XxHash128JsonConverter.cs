using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Primitives.JsonConverters;

/// <summary>
/// Converts an <see cref="XxHash128"/> to or from JSON as a 32-character hexadecimal string.
/// </summary>
public sealed class XxHash128JsonConverter : JsonConverter<XxHash128> {
    /// <inheritdoc/>
    public override XxHash128 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected string token for XxHash128, got {reader.TokenType}.");
        }

        ReadOnlySpan<byte> utf8Text = reader.HasValueSequence ? BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan;
        if(!XxHash128.TryParse(utf8Text, out XxHash128 result)) {
            throw new JsonException("Failed to parse XxHash128 from JSON string.");
        }

        return result;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, XxHash128 value, JsonSerializerOptions options) {
        Span<byte> buffer = stackalloc byte[XxHash128.HashSizeInBytes * 2];
        value.TryFormat(buffer, out int written);
        writer.WriteStringValue(buffer[..written]);
    }
}
