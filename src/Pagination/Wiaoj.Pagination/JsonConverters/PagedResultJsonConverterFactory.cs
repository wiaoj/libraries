using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// Factory that creates specialized <see cref="JsonConverter{T}"/> instances for <see cref="PagedResult{T}"/>.
/// </summary>
public sealed class PagedResultJsonConverterFactory : JsonConverterFactory {
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(PagedResult<>);
    }

    /// <inheritdoc/>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type itemType = typeToConvert.GetGenericArguments()[0];

        Type converterType = typeof(PagedResultJsonConverter<>).MakeGenericType(itemType);

        return (JsonConverter?)Activator.CreateInstance(
            converterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null);
    }
}

/// <summary>
/// Specialized JSON converter for <see cref="PagedResult{T}"/>.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
internal sealed class PagedResultJsonConverter<T> : JsonConverter<PagedResult<T>> {
    private static readonly JsonEncodedText ItemsPropertyName = JsonEncodedText.Encode("items");
    private static readonly JsonEncodedText MetadataPropertyName = JsonEncodedText.Encode("metadata");

    /// <inheritdoc/>
    public override PagedResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("Expected StartObject token for PagedResult.");
        }

        EquatableArray<T> items = [];
        PageMetadata metadata = PageMetadata.Empty;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) {
                return new PagedResult<T>(items, metadata);
            }

            if(reader.TokenType != JsonTokenType.PropertyName) {
                continue;
            }

            if(reader.ValueTextEquals(ItemsPropertyName.EncodedUtf8Bytes)) {
                reader.Read();
                items = JsonSerializer.Deserialize<EquatableArray<T>>(ref reader, options);
            }
            else if(reader.ValueTextEquals(MetadataPropertyName.EncodedUtf8Bytes)) {
                reader.Read();
                metadata = JsonSerializer.Deserialize<PageMetadata>(ref reader, options);
            }
            else {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON while parsing PagedResult.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PagedResult<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        writer.WritePropertyName(ItemsPropertyName);
        JsonSerializer.Serialize(writer, value.Items, options);

        writer.WritePropertyName(MetadataPropertyName);
        JsonSerializer.Serialize(writer, value.Metadata, options);

        writer.WriteEndObject();
    }
}