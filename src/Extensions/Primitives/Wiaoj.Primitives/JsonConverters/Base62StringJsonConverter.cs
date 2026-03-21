using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters; 
/// <summary>JSON converter for <see cref="Base62String"/>.</summary>
public sealed class Base62StringJsonConverter : JsonConverter<Base62String> { 
    /// <inheritdoc/>
    public override Base62String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            // Try to read directly from UTF-8 span for performance
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            if(Base62String.TryParse(span, out Base62String result)) {
                return result;
            }

            // If the string is invalid Base62, we should throw to inform the deserializer
            throw new JsonException($"The value '{reader.GetString()}' is not a valid Base62 string.");
        }

        throw new JsonException($"Unexpected token type '{reader.TokenType}' for Base62String.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base62String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}