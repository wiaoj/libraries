using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// A custom JsonConverter for serializing and deserializing the <see cref="Base32String"/> struct.
/// </summary>
public sealed class Base32StringJsonConverter : JsonConverter<Base32String> { 
    /// <inheritdoc/>
    public override Base32String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            if (!reader.ValueIsEscaped) {
                // Span olarak al (String allocation yapmadan)
                ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

                if(Base32String.TryParse(span, out Base32String result)) {
                    return result;
                }
            } else {
                string? text = reader.GetString();
                if (text != null && Base32String.TryParse(text, out Base32String result)) {
                    return result;
                }
            }

            throw new JsonException($"The value '{reader.GetString()}' is not a valid Base32 string.");
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for Base32String.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base32String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}