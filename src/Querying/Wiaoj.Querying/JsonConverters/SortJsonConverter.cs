using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Querying.JsonConverters;

/// <summary>
/// JSON converter for <see cref="Sort"/> serializing and deserializing as a string expression.
/// </summary>
public sealed class SortJsonConverter : JsonConverter<Sort> {
    /// <inheritdoc/>
    public override Sort Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return Sort.Empty;
        }

        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected string token for Sort, got '{reader.TokenType}'.");
        }

        string? str = reader.GetString();
        return string.IsNullOrWhiteSpace(str) ? Sort.Empty : new Sort(str);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Sort value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString());
    }
}