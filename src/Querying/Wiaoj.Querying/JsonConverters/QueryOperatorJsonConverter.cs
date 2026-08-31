using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.JsonConverters;

/// <summary>
/// JSON converter for <see cref="QueryOperator"/> mapping between enum values and query syntax operator strings.
/// </summary>
public sealed class QueryOperatorJsonConverter : JsonConverter<QueryOperator> {
    /// <inheritdoc/>
    public override QueryOperator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected string token for QueryOperator, got '{reader.TokenType}'.");
        }

        string? opString = reader.GetString();
        if(string.IsNullOrWhiteSpace(opString)) {
            throw new JsonException("QueryOperator string cannot be null or whitespace.");
        }

        return opString.ToLowerInvariant() switch {
            QuerySyntax.Operators.Equal => QueryOperator.Equal,
            QuerySyntax.Operators.NotEqual => QueryOperator.NotEqual,
            QuerySyntax.Operators.GreaterThan => QueryOperator.GreaterThan,
            QuerySyntax.Operators.GreaterThanOrEqual => QueryOperator.GreaterThanOrEqual,
            QuerySyntax.Operators.LessThan => QueryOperator.LessThan,
            QuerySyntax.Operators.LessThanOrEqual => QueryOperator.LessThanOrEqual,
            QuerySyntax.Operators.Contains => QueryOperator.Contains,
            QuerySyntax.Operators.NotContains => QueryOperator.NotContains,
            QuerySyntax.Operators.StartsWith => QueryOperator.StartsWith,
            QuerySyntax.Operators.NotStartsWith => QueryOperator.NotStartsWith,
            QuerySyntax.Operators.EndsWith => QueryOperator.EndsWith,
            QuerySyntax.Operators.NotEndsWith => QueryOperator.NotEndsWith,
            QuerySyntax.Operators.In => QueryOperator.In,
            QuerySyntax.Operators.NotIn => QueryOperator.NotIn,
            QuerySyntax.Operators.Between => QueryOperator.Between,
            QuerySyntax.Operators.NotBetween => QueryOperator.NotBetween,
            QuerySyntax.Operators.IsNull => QueryOperator.IsNull,
            QuerySyntax.Operators.IsNotNull => QueryOperator.IsNotNull,
            _ => throw new JsonException($"Unknown query operator '{opString}'.")
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, QueryOperator value, JsonSerializerOptions options) {
        string str = value switch {
            QueryOperator.Equal => QuerySyntax.Operators.Equal,
            QueryOperator.NotEqual => QuerySyntax.Operators.NotEqual,
            QueryOperator.GreaterThan => QuerySyntax.Operators.GreaterThan,
            QueryOperator.GreaterThanOrEqual => QuerySyntax.Operators.GreaterThanOrEqual,
            QueryOperator.LessThan => QuerySyntax.Operators.LessThan,
            QueryOperator.LessThanOrEqual => QuerySyntax.Operators.LessThanOrEqual,
            QueryOperator.Contains => QuerySyntax.Operators.Contains,
            QueryOperator.NotContains => QuerySyntax.Operators.NotContains,
            QueryOperator.StartsWith => QuerySyntax.Operators.StartsWith,
            QueryOperator.NotStartsWith => QuerySyntax.Operators.NotStartsWith,
            QueryOperator.EndsWith => QuerySyntax.Operators.EndsWith,
            QueryOperator.NotEndsWith => QuerySyntax.Operators.NotEndsWith,
            QueryOperator.In => QuerySyntax.Operators.In,
            QueryOperator.NotIn => QuerySyntax.Operators.NotIn,
            QueryOperator.Between => QuerySyntax.Operators.Between,
            QueryOperator.NotBetween => QuerySyntax.Operators.NotBetween,
            QueryOperator.IsNull => QuerySyntax.Operators.IsNull,
            QueryOperator.IsNotNull => QuerySyntax.Operators.IsNotNull,
            _ => throw new JsonException($"Unsupported query operator value '{(byte)value}'.")
        };
        writer.WriteStringValue(str);
    }
}