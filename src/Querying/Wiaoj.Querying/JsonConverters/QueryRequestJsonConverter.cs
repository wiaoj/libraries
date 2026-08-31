using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Querying.JsonConverters;

/// <summary>
/// JSON converter for <see cref="QueryRequest"/> respecting user-configured naming policies and ignore conditions.
/// </summary>
public sealed class QueryRequestJsonConverter : JsonConverter<QueryRequest> {
    private static readonly QJsonConverter QConverter = new();
    private static readonly SortJsonConverter SortConverter = new();
    private static readonly FilterConditionNodeJsonConverter FilterConverter = new();

    /// <inheritdoc/>
    public override QueryRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException($"Expected object token for QueryRequest, got '{reader.TokenType}'.");
        }

        Q q = default;
        Sort sort = default;
        List<FilterConditionNode>? filters = null;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) {
                break;
            }

            if(reader.TokenType == JsonTokenType.PropertyName) {
                string? propertyName = reader.GetString();
                reader.Read();

                if(string.Equals(propertyName, "q", StringComparison.OrdinalIgnoreCase)) {
                    q = QConverter.Read(ref reader, typeof(Q), options);
                }
                else if(string.Equals(propertyName, "sort", StringComparison.OrdinalIgnoreCase)) {
                    sort = SortConverter.Read(ref reader, typeof(Sort), options);
                }
                else if(string.Equals(propertyName, "filters", StringComparison.OrdinalIgnoreCase)) {
                    if(reader.TokenType == JsonTokenType.StartArray) {
                        filters = [];
                        while(reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                            filters.Add(FilterConverter.Read(ref reader, typeof(FilterConditionNode), options));
                        }
                    }
                }
                else {
                    reader.Skip();
                }
            }
        }

        return new QueryRequest(q: q, sort: sort, filters: filters);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, QueryRequest value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        if(!value.Q.IsEmpty) {
            string qPropName = GetPropertyName("q", options);
            writer.WritePropertyName(qPropName);
            QConverter.Write(writer, value.Q, options);
        }

        if(!value.Sort.IsEmpty) {
            string sortPropName = GetPropertyName("sort", options);
            writer.WritePropertyName(sortPropName);
            SortConverter.Write(writer, value.Sort, options);
        }

        if(value.Filters.Count > 0) {
            string filtersPropName = GetPropertyName("filters", options);
            writer.WritePropertyName(filtersPropName);
            writer.WriteStartArray();
            for(int i = 0; i < value.Filters.Count; i++) {
                FilterConverter.Write(writer, value.Filters[i], options);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static string GetPropertyName(string name, JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(name) ?? name;
}