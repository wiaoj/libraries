using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.JsonConverters;

/// <summary>
/// Factory that creates specialized <see cref="JsonConverter{T}"/> instances for <see cref="CursorResult{T}"/>.
/// </summary>
public sealed class CursorResultJsonConverterFactory : JsonConverterFactory {
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(CursorResult<>);
    }

    /// <inheritdoc/>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        Type itemType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(CursorResultJsonConverter<>).MakeGenericType(itemType);

        return (JsonConverter?)Activator.CreateInstance(
            converterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null);
    }
}

internal sealed class CursorResultJsonConverter<T> : JsonConverter<CursorResult<T>> {
    private static readonly JsonEncodedText ItemsPropertyName = JsonEncodedText.Encode("items");
    private static readonly JsonEncodedText MetadataPropertyName = JsonEncodedText.Encode("metadata");

    /// <inheritdoc/>
    public override CursorResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("Expected StartObject token for CursorResult.");
        }

        EquatableArray<T> items = [];
        CursorMetadata metadata = CursorMetadata.Empty;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) {
                return new CursorResult<T>(items, metadata);
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
                metadata = JsonSerializer.Deserialize<CursorMetadata>(ref reader, options);
            }
            else {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON while parsing CursorResult.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CursorResult<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        writer.WritePropertyName(ItemsPropertyName);
        JsonSerializer.Serialize(writer, value.Items, options);

        writer.WritePropertyName(MetadataPropertyName);
        JsonSerializer.Serialize(writer, value.Metadata, options);

        writer.WriteEndObject();
    }
}