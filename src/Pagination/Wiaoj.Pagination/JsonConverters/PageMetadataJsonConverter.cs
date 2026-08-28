
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// High-performance, zero-allocation JSON converter for <see cref="PageMetadata"/>.
/// </summary>
public sealed class PageMetadataJsonConverter : JsonConverter<PageMetadata> {
    private static readonly JsonEncodedText TotalCountName = JsonEncodedText.Encode("totalCount");
    private static readonly JsonEncodedText PageNumberName = JsonEncodedText.Encode("pageNumber");
    private static readonly JsonEncodedText PageSizeName = JsonEncodedText.Encode("pageSize");
    private static readonly JsonEncodedText TotalPagesName = JsonEncodedText.Encode("totalPages");
    private static readonly JsonEncodedText HasPreviousName = JsonEncodedText.Encode("hasPrevious");
    private static readonly JsonEncodedText HasNextName = JsonEncodedText.Encode("hasNext");

    /// <inheritdoc/>
    public override PageMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("Expected StartObject token.");
        }

        int totalCount = 0;
        int pageNumber = 1;
        int pageSize = 1;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) {
                return new PageMetadata(totalCount, pageNumber, pageSize);
            }

            if(reader.TokenType != JsonTokenType.PropertyName) {
                continue;
            }

            if(reader.ValueTextEquals(TotalCountName.EncodedUtf8Bytes)) {
                reader.Read();
                totalCount = reader.GetInt32();
            }
            else if(reader.ValueTextEquals(PageNumberName.EncodedUtf8Bytes)) {
                reader.Read();
                pageNumber = reader.GetInt32();
            }
            else if(reader.ValueTextEquals(PageSizeName.EncodedUtf8Bytes)) {
                reader.Read();
                pageSize = reader.GetInt32();
            }
            else {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON while parsing PageMetadata.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PageMetadata value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteNumber(TotalCountName, value.TotalCount);
        writer.WriteNumber(PageNumberName, value.PageNumber);
        writer.WriteNumber(PageSizeName, value.PageSize);
        writer.WriteNumber(TotalPagesName, value.TotalPages);
        writer.WriteBoolean(HasPreviousName, value.HasPrevious);
        writer.WriteBoolean(HasNextName, value.HasNext);
        writer.WriteEndObject();
    }
}