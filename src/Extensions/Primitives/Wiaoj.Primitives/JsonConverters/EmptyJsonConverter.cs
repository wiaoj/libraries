using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;
/// <summary>
/// Empty Json Converter
/// </summary>
public sealed class EmptyJsonConverter : JsonConverter<Empty> {
    /// <inheritdoc/>
    public override Empty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // (null, {}, string)
        reader.Skip();
        return Empty.Default;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Empty value, JsonSerializerOptions options) {
        // "{}"
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}