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
            // Span olarak al (String allocation yapmadan)
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            // Base32String.TryParse (Byte versiyonu) kullanıyoruz.
            // Bu metot explicit implemente edilmiş olsa bile, converter içinde Base32String'e cast ederek veya 
            // internal bir metoda erişerek (aynı assembly ise) kullanılabilir.
            // Ancak senin kodunda Base32String.TryParse(ReadOnlySpan<byte>) public idi, onu kullanıyoruz.
            if(Base32String.TryParse(span, out Base32String result)) {
                return result;
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