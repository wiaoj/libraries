using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// A custom JsonConverter for serializing and deserializing the <see cref="Base64String"/> struct efficiently.
/// </summary>
public sealed class Base64StringJsonConverter : JsonConverter<Base64String> { 
    /// <inheritdoc/>
    public override Base64String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            // Optimization: Try to read directly from the ValueSpan (raw bytes) if possible
            // to avoid allocating an intermediate string before validation.
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            // Direct call to the public static TryParse(ReadOnlySpan<byte>...) method.
            // This validates and creates the object without string allocation if the input is valid UTF-8 bytes.
            if(Base64String.TryParse(span, out Base64String result)) {
                return result;
            }
        }

        throw new JsonException($"Unable to convert JSON token {reader.TokenType} to Base64String. Ensure the string contains valid Base64 characters.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base64String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}