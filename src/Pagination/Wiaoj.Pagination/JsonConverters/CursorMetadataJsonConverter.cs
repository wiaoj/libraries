using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// High-performance, zero-allocation JSON converter for <see cref="CursorMetadata"/>.
/// </summary>
public sealed class CursorMetadataJsonConverter : JsonConverter<CursorMetadata> {
    private static readonly JsonEncodedText StartCursorName = JsonEncodedText.Encode("startCursor");
    private static readonly JsonEncodedText EndCursorName = JsonEncodedText.Encode("endCursor");
    private static readonly JsonEncodedText HasPreviousName = JsonEncodedText.Encode("hasPrevious");
    private static readonly JsonEncodedText HasNextName = JsonEncodedText.Encode("hasNext");

    /// <inheritdoc/>
    public override CursorMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("Expected StartObject token.");
        }

        CursorToken startCursor = CursorToken.Empty;
        CursorToken endCursor = CursorToken.Empty;
        bool hasPrevious = false;
        bool hasNext = false;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) {
                return new CursorMetadata(startCursor, endCursor, hasPrevious, hasNext);
            }

            if(reader.TokenType != JsonTokenType.PropertyName) {
                continue;
            }

            if(reader.ValueTextEquals(StartCursorName.EncodedUtf8Bytes)) {
                reader.Read();
                if(reader.TokenType == JsonTokenType.String && !reader.HasValueSequence) {
                    startCursor = CursorToken.Parse(reader.ValueSpan);
                }
            }
            else if(reader.ValueTextEquals(EndCursorName.EncodedUtf8Bytes)) {
                reader.Read();
                if(reader.TokenType == JsonTokenType.String && !reader.HasValueSequence) {
                    endCursor = CursorToken.Parse(reader.ValueSpan);
                }
            }
            else if(reader.ValueTextEquals(HasPreviousName.EncodedUtf8Bytes)) {
                reader.Read();
                hasPrevious = reader.GetBoolean();
            }
            else if(reader.ValueTextEquals(HasNextName.EncodedUtf8Bytes)) {
                reader.Read();
                hasNext = reader.GetBoolean();
            }
            else {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON while parsing CursorMetadata.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CursorMetadata value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        writer.WritePropertyName(StartCursorName);
        if(value.StartCursor.IsEmpty) {
            writer.WriteNullValue();
        }
        else {
            writer.WriteStringValue(value.StartCursor.Value.AsSpan());
        }

        writer.WritePropertyName(EndCursorName);
        if(value.EndCursor.IsEmpty) {
            writer.WriteNullValue();
        }
        else {
            writer.WriteStringValue(value.EndCursor.Value.AsSpan());
        }

        writer.WriteBoolean(HasPreviousName, value.HasPrevious);
        writer.WriteBoolean(HasNextName, value.HasNext);
        writer.WriteEndObject();
    }
}