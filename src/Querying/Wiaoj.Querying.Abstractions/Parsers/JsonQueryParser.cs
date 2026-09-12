using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Parsers;

/// <summary>
/// High-performance, AOT-compliant parser resolving <see cref="QueryRequest"/> instances from JSON payloads.
/// </summary>
public static class JsonQueryParser {
    private const int StackallocThreshold = 512;
    private const int MaxUtf8Length = 64 * 1024; // 64 KB protection limit

    private static readonly JsonReaderOptions DefaultReaderOptions = new() {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 16
    };

    private static ReadOnlySpan<byte> PropertyQ => "q"u8;
    private static ReadOnlySpan<byte> PropertySort => "sort"u8;
    private static ReadOnlySpan<byte> PropertyFilters => "filters"u8;
    private static ReadOnlySpan<byte> PropertyField => "field"u8;
    private static ReadOnlySpan<byte> PropertyOp => "op"u8;
    private static ReadOnlySpan<byte> PropertyValue => "value"u8;

    /// <summary>
    /// Attempts to parse a JSON query payload string into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="QueryRequest.Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? json, out QueryRequest result) {
        if(string.IsNullOrWhiteSpace(json)) {
            result = QueryRequest.Empty;
            return false;
        }

        return TryParse(json.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a JSON query payload character span into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="json">The JSON character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="QueryRequest.Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> json, out QueryRequest result) {
        ReadOnlySpan<char> trimmed = json.Trim();
        if(trimmed.IsEmpty) {
            result = QueryRequest.Empty;
            return false;
        }

        int maxByteCount = Encoding.UTF8.GetMaxByteCount(trimmed.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[StackallocThreshold]);

        if(Utf8.FromUtf16(trimmed, buffer.Span, out _, out int bytesWritten) != OperationStatus.Done) {
            result = QueryRequest.Empty;
            return false;
        }

        return TryParse(buffer.Span[..bytesWritten], out result);
    }

    /// <summary>
    /// Attempts to parse a JSON query payload UTF-8 byte span into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="utf8Json">The JSON UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="QueryRequest.Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Json, out QueryRequest result) {
        result = QueryRequest.Empty;

        if(utf8Json.IsEmpty || utf8Json.Length > MaxUtf8Length) {
            return false;
        }

        try {
            Utf8JsonReader reader = new(utf8Json, DefaultReaderOptions);

            if(!reader.Read() || reader.TokenType != JsonTokenType.StartObject) {
                return false;
            }

            Q q = default;
            Sort sort = default;
            List<FilterConditionNode>? filters = null;

            while(reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
                if(reader.TokenType != JsonTokenType.PropertyName) {
                    return false;
                }

                ReadOnlySpan<byte> propName = reader.ValueSpan;

                if(Ascii.EqualsIgnoreCase(propName, PropertyQ)) {
                    if(!reader.Read() || reader.TokenType != JsonTokenType.String) {
                        return false;
                    }
                    q = new Q(reader.GetString());
                }
                else if(Ascii.EqualsIgnoreCase(propName, PropertySort)) {
                    if(!reader.Read() || reader.TokenType != JsonTokenType.String) {
                        return false;
                    }
                    if(!Sort.TryParse(reader.GetString(), out Sort parsedSort)) {
                        return false;
                    }
                    sort = parsedSort;
                }
                else if(Ascii.EqualsIgnoreCase(propName, PropertyFilters)) {
                    if(!reader.Read() || reader.TokenType != JsonTokenType.StartArray) {
                        return false;
                    }

                    filters = ParseFiltersArray(ref reader);
                    if(filters is null) {
                        return false;
                    }
                }
                else {
                    reader.Skip();
                }
            }

            result = new QueryRequest(q: q, sort: sort, filters: filters);
            return true;
        }
        catch {
            result = QueryRequest.Empty;
            return false;
        }
    }

    private static List<FilterConditionNode>? ParseFiltersArray(ref Utf8JsonReader reader) {
        List<FilterConditionNode> list = [];

        while(reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            if(reader.TokenType != JsonTokenType.StartObject) {
                return null;
            }

            string? field = null;
            QueryOperator op = QueryOperator.Equal;
            string? rawValue = null;
            bool opExplicitlySet = false;

            while(reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
                if(reader.TokenType != JsonTokenType.PropertyName) {
                    return null;
                }

                ReadOnlySpan<byte> propName = reader.ValueSpan;

                if(Ascii.EqualsIgnoreCase(propName, PropertyField)) {
                    if(!reader.Read() || reader.TokenType != JsonTokenType.String) {
                        return null;
                    }
                    field = reader.GetString();
                }
                else if(Ascii.EqualsIgnoreCase(propName, PropertyOp)) {
                    if(!reader.Read() || reader.TokenType != JsonTokenType.String) {
                        return null;
                    }
                    string? opStr = reader.GetString();
                    if(opStr != null && TryMapOperator(opStr.AsSpan(), out QueryOperator mappedOp)) {
                        op = mappedOp;
                        opExplicitlySet = true;
                    }
                    else {
                        return null;
                    }
                }
                else if(Ascii.EqualsIgnoreCase(propName, PropertyValue)) {
                    if(!reader.Read()) {
                        return null;
                    }
                    rawValue = reader.TokenType switch {
                        JsonTokenType.String => reader.GetString(),
                        JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
                        JsonTokenType.True => bool.TrueString.ToLowerInvariant(),
                        JsonTokenType.False => bool.FalseString.ToLowerInvariant(),
                        JsonTokenType.Null => null,
                        _ => null
                    };

                    if(rawValue is null && reader.TokenType is not (JsonTokenType.Null or JsonTokenType.String)) {
                        return null;
                    }
                }
                else {
                    reader.Skip();
                }
            }

            if(string.IsNullOrWhiteSpace(field)) {
                return null;
            }

            if(!opExplicitlySet) {
                op = QueryOperator.Equal;
            }

            if(op is QueryOperator.IsNull or QueryOperator.IsNotNull) {
                rawValue = null;
            }

            list.Add(new FilterConditionNode(field.Trim(), op, rawValue));
        }

        return list;
    }

    private static bool TryMapOperator(ReadOnlySpan<char> opSpan, out QueryOperator queryOperator) {
        queryOperator = opSpan switch {
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.Equal) => QueryOperator.Equal,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotEqual) => QueryOperator.NotEqual,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.GreaterThan) => QueryOperator.GreaterThan,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.GreaterThanOrEqual) => QueryOperator.GreaterThanOrEqual,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.LessThan) => QueryOperator.LessThan,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.LessThanOrEqual) => QueryOperator.LessThanOrEqual,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.Contains) => QueryOperator.Contains,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotContains) => QueryOperator.NotContains,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.StartsWith) => QueryOperator.StartsWith,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotStartsWith) => QueryOperator.NotStartsWith,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.EndsWith) => QueryOperator.EndsWith,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotEndsWith) => QueryOperator.NotEndsWith,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.In) => QueryOperator.In,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotIn) => QueryOperator.NotIn,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.Between) => QueryOperator.Between,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotBetween) => QueryOperator.NotBetween,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.IsNull) => QueryOperator.IsNull,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.IsNotNull) => QueryOperator.IsNotNull,
            _ => default
        };

        return queryOperator != default;
    }
}