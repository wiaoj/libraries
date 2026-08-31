using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Querying.JsonConverters;

/// <summary>
/// JSON converter for <see cref="FilterConditionNode"/> respecting user-configured naming policies and ignore conditions.
/// </summary>
public sealed class FilterConditionNodeJsonConverter : JsonConverter<FilterConditionNode> {
    private static readonly QueryOperatorJsonConverter OpConverter = new();

    /// <inheritdoc/>
    public override FilterConditionNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException($"Expected object token for FilterConditionNode, got '{reader.TokenType}'.");
        }

        string? field = null;
        QueryOperator op = default;
        string? rawValue = null;
        bool opFound = false;

        while(reader.Read()) {
            if(reader.TokenType == JsonTokenType.EndObject) {
                break;
            }

            if(reader.TokenType == JsonTokenType.PropertyName) {
                string? propertyName = reader.GetString();
                reader.Read();

                if(string.Equals(propertyName, "field", StringComparison.OrdinalIgnoreCase)) {
                    field = reader.GetString();
                }
                else if(string.Equals(propertyName, "operator", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propertyName, "op", StringComparison.OrdinalIgnoreCase)) {
                    op = OpConverter.Read(ref reader, typeof(QueryOperator), options);
                    opFound = true;
                }
                else if(string.Equals(propertyName, "rawValue", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propertyName, "raw_value", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propertyName, "raw-value", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase)) {
                    if(reader.TokenType != JsonTokenType.Null) {
                        rawValue = reader.GetString();
                    }
                }
                else {
                    reader.Skip();
                }
            }
        }

        if(string.IsNullOrWhiteSpace(field)) {
            throw new JsonException("FilterConditionNode requires a non-empty 'field' property.");
        }

        if(!opFound) {
            throw new JsonException("FilterConditionNode requires a valid 'operator' property.");
        }

        return new FilterConditionNode(field, op, rawValue);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FilterConditionNode value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        string fieldPropName = GetPropertyName("field", options);
        writer.WriteString(fieldPropName, value.Field);

        string opPropName = GetPropertyName("operator", options);
        writer.WritePropertyName(opPropName);
        OpConverter.Write(writer, value.Operator, options);

        if(value.RawValue is not null) {
            string rawValuePropName = GetPropertyName("rawValue", options);
            writer.WriteString(rawValuePropName, value.RawValue);
        }
        else if(options.DefaultIgnoreCondition != JsonIgnoreCondition.WhenWritingNull) {
            string rawValuePropName = GetPropertyName("rawValue", options);
            writer.WriteNull(rawValuePropName);
        }

        writer.WriteEndObject();
    }

    private static string GetPropertyName(string name, JsonSerializerOptions options) {
        return options.PropertyNamingPolicy?.ConvertName(name) ?? name;
    }
}