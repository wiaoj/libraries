using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters; 
/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing the <see cref="Base64UrlString"/> struct.
/// </summary>
public sealed class Base64UrlStringJsonConverter : JsonConverter<Base64UrlString> {

    /// <inheritdoc/>
    public override Base64UrlString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Unexpected token type {reader.TokenType} for Base64UrlString.");
        }

        if(!reader.HasValueSequence && !reader.ValueIsEscaped) {
            if(Base64UrlString.TryParse(reader.ValueSpan, out Base64UrlString result)) {
                return result;
            }
        }
        else {
            string? s = reader.GetString();
            if(s is not null && Base64UrlString.TryParse(s, out Base64UrlString result)) {
                return result;
            }
        }

        throw new JsonException("The JSON string is not a valid Base64Url format.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base64UrlString value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}