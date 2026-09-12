using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.JsonConverters;

/// <summary>
/// Serialises a <see cref="QueryRequest"/> as the same JSON body a <c>QUERY</c> or <c>POST</c> request sends.
/// </summary>
/// <remarks>
/// <para>
/// Without a converter the type did not survive System.Text.Json at all. Its <c>Sort</c> is a read-only
/// collection, so deserialising any request — even one with no sort, written as <c>"sort":[]</c> — threw.
/// Writing it leaked computed members (<c>isEmpty</c>, <c>length</c>, <c>queryHash</c>) and wrote operators as
/// numbers. For a type whose purpose is to cross a service boundary inside a contract, that ruled out every
/// JSON transport.
/// </para>
/// <para>
/// The shape is the one <see cref="JsonQueryParser"/> already reads:
/// </para>
/// <code>
/// { "q": "hello", "sort": "-locale,keyId", "filters": [ { "field": "keyId", "op": "in", "value": "k1,k3" } ] }
/// </code>
/// <para>
/// Reading is delegated to that parser, so there is one JSON grammar for queries rather than two that can
/// drift. Property names are fixed and do not follow the serializer's naming policy: this is a wire contract
/// between two services, and it must not change shape because one of them configured camelCase differently.
/// </para>
/// <para>
/// A payload the parser rejects throws <see cref="JsonException"/>. It is never read as an empty request — an
/// empty request applies no filters, so a malformed query would silently become "return everything".
/// </para>
/// </remarks>
public sealed class QueryRequestJsonConverter : JsonConverter<QueryRequest> {
    private static readonly JsonEncodedText QName = JsonEncodedText.Encode("q");
    private static readonly JsonEncodedText SortName = JsonEncodedText.Encode("sort");
    private static readonly JsonEncodedText FiltersName = JsonEncodedText.Encode("filters");
    private static readonly JsonEncodedText FieldName = JsonEncodedText.Encode("field");
    private static readonly JsonEncodedText OpName = JsonEncodedText.Encode("op");
    private static readonly JsonEncodedText ValueName = JsonEncodedText.Encode("value");

    /// <inheritdoc/>
    public override QueryRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.Null) {
            return QueryRequest.Empty;
        }

        if(reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException($"Expected a JSON object for {nameof(QueryRequest)}, found {reader.TokenType}.");
        }

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        ReadOnlySpan<byte> raw = JsonMarshal.GetRawUtf8Value(document.RootElement);

        return JsonQueryParser.TryParse(raw, out QueryRequest request)
            ? request
            : throw new JsonException(
                $"The payload is not a valid {nameof(QueryRequest)}. It is rejected rather than read as an empty " +
                "request, which would apply no filters.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, QueryRequest value, JsonSerializerOptions options) {
        writer.WriteStartObject();

        if(!value.Q.IsEmpty) {
            writer.WriteString(QName, value.Q.Value);
        }

        if(!value.Sort.IsEmpty) {
            writer.WriteString(SortName, value.Sort.ToString());
        }

        if(value.Filters.Count > 0) {
            writer.WriteStartArray(FiltersName);

            foreach(FilterConditionNode filter in value.Filters) {
                if(filter.IsEmpty) {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString(FieldName, filter.Field);
                writer.WriteString(OpName, QuerySyntax.GetOperatorToken(filter.Operator));

                if(!filter.IsUnary && filter.RawValue is not null) {
                    writer.WriteString(ValueName, filter.RawValue);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
