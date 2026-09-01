
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// High-performance, zero-allocation JSON converter for <see cref="PageMetadata"/>.
/// </summary>
public sealed class PageMetadataJsonConverter : JsonConverter<PageMetadata> {
    private static readonly JsonEncodedText TotalCountName = JsonEncodedText.Encode("totalCount");
    private static readonly JsonEncodedText PageName = JsonEncodedText.Encode("page");
    private static readonly JsonEncodedText SizeName = JsonEncodedText.Encode("size");
    private static readonly JsonEncodedText TotalPagesName = JsonEncodedText.Encode("totalPages");
    private static readonly JsonEncodedText HasPreviousName = JsonEncodedText.Encode("hasPrevious");
    private static readonly JsonEncodedText HasNextName = JsonEncodedText.Encode("hasNext");

    /// <inheritdoc/>
    public override PageMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("Expected StartObject token.");
        }

        long totalCount = 0;
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
                totalCount = reader.GetInt64();
            }
            else if(reader.ValueTextEquals(PageName.EncodedUtf8Bytes)) {
                reader.Read();
                pageNumber = reader.GetInt32();
            }
            else if(reader.ValueTextEquals(SizeName.EncodedUtf8Bytes)) {
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
        writer.WriteNumber(PageName, value.Page);
        writer.WriteNumber(SizeName, value.Size);
        writer.WriteNumber(TotalPagesName, value.TotalPages);
        writer.WriteBoolean(HasPreviousName, value.HasPrevious);
        writer.WriteBoolean(HasNextName, value.HasNext);
        writer.WriteEndObject();
    }
}