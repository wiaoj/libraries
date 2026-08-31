using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Querying.JsonConverters;

/// <summary>
/// JSON converter for <see cref="Q"/> serializing and deserializing as a primitive JSON string.
/// </summary>
public sealed class QJsonConverter : JsonConverter<Q> {
    /// <inheritdoc/>
    public override Q Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return Q.Empty;
        }

        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected string token for Q, got '{reader.TokenType}'.");
        }

        return new Q(reader.GetString());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Q value, JsonSerializerOptions options) {
        if(value.IsEmpty) {
            writer.WriteStringValue(string.Empty);
        }
        else {
            writer.WriteStringValue(value.Value);
        }
    }
}