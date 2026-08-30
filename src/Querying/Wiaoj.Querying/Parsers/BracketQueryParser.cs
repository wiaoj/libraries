using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Parsers;
/// <summary>
/// Parses bracket-style query parameters into <see cref="FilterConditionNode"/> instances.
/// </summary>
internal sealed class BracketQueryParser {
    /// <summary>
    /// Attempts to parse a single query key-value pair into a <see cref="FilterConditionNode"/>.
    /// </summary>
    /// <param name="input">The raw query parameter span (e.g., <c>price[gte]=100</c>, <c>deletedAt[isNull]</c>).</param>
    /// <param name="result">The parsed condition node if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryParse(ReadOnlySpan<char> input, out FilterConditionNode result) {
        result = default;

        ReadOnlySpan<char> trimmed = input.Trim();
        if(trimmed.IsEmpty) {
            return false;
        }

        var equalsIndex = trimmed.IndexOf(QuerySyntax.KeyValueDelimiter);

        // Case 1: No '=' delimiter present (e.g. "deletedAt[isNull]" or malformed "price[gte]")
        if(equalsIndex == -1) {
            return TryParseWithoutValue(trimmed, out result);
        }

        // Case 2: '=' is at the start (e.g. "=100")
        if(equalsIndex == 0) {
            return false;
        }

        ReadOnlySpan<char> keySpan = trimmed[..equalsIndex].Trim();
        ReadOnlySpan<char> valueSpan = trimmed[(equalsIndex + 1)..].Trim();

        if(keySpan.IsEmpty) {
            return false;
        }

        var openBracketIndex = keySpan.IndexOf(QuerySyntax.OpenBracket);
        var closeBracketIndex = keySpan.IndexOf(QuerySyntax.CloseBracket);

        // Case 2A: Implicit equality without brackets (e.g., "status=Active")
        if(openBracketIndex == -1 && closeBracketIndex == -1) {
            result = new FilterConditionNode(
                Field: keySpan.ToString(),
                Operator: QueryOperator.Equal,
                RawValue: valueSpan.ToString());

            return true;
        }

        // Case 2B: Validate bracket structure
        if(!IsValidBracketStructure(keySpan, openBracketIndex, closeBracketIndex)) {
            return false;
        }

        ReadOnlySpan<char> fieldSpan = keySpan[..openBracketIndex].Trim();
        ReadOnlySpan<char> opSpan = keySpan.Slice(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

        if(fieldSpan.IsEmpty || opSpan.IsEmpty) {
            return false;
        }

        if(!TryMapOperator(opSpan, out QueryOperator queryOperator)) {
            return false;
        }

        // Unary operators (isNull, isNotNull) do not require a value even if '=' is provided
        string? rawValue = IsUnaryOperator(queryOperator)
            ? null
            : valueSpan.ToString();

        result = new FilterConditionNode(
            Field: fieldSpan.ToString(),
            Operator: queryOperator,
            RawValue: rawValue);

        return true;
    }

    private static bool TryParseWithoutValue(ReadOnlySpan<char> trimmed, out FilterConditionNode result) {
        result = default;

        var openBracketIndex = trimmed.IndexOf(QuerySyntax.OpenBracket);
        var closeBracketIndex = trimmed.IndexOf(QuerySyntax.CloseBracket);

        if(!IsValidBracketStructure(trimmed, openBracketIndex, closeBracketIndex)) {
            return false;
        }

        ReadOnlySpan<char> fieldSpan = trimmed[..openBracketIndex].Trim();
        ReadOnlySpan<char> opSpan = trimmed.Slice(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

        if(fieldSpan.IsEmpty || opSpan.IsEmpty) {
            return false;
        }

        if(!TryMapOperator(opSpan, out QueryOperator queryOperator)) {
            return false;
        }

        // Only unary operators (isNull, isNotNull) are valid without an '=' and value
        if(!IsUnaryOperator(queryOperator)) {
            return false;
        }

        result = new FilterConditionNode(
            Field: fieldSpan.ToString(),
            Operator: queryOperator,
            RawValue: null);

        return true;
    }

    private static bool IsValidBracketStructure(ReadOnlySpan<char> span, int openIndex, int closeIndex) {
        // Must contain valid bracket positions
        if(openIndex <= 0 || closeIndex <= openIndex + 1 || closeIndex != span.Length - 1) {
            return false;
        }

        // Ensure there are no duplicate brackets (e.g., "field[[op]]=1" or "field[op][extra]=1")
        return span.LastIndexOf(QuerySyntax.OpenBracket) == openIndex &&
               span.LastIndexOf(QuerySyntax.CloseBracket) == closeIndex;
    }

    private static bool IsUnaryOperator(QueryOperator queryOperator) {
        return queryOperator is QueryOperator.IsNull or QueryOperator.IsNotNull;
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
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.StartsWith) => QueryOperator.StartsWith,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.EndsWith) => QueryOperator.EndsWith,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.In) => QueryOperator.In,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.NotIn) => QueryOperator.NotIn,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.Between) => QueryOperator.Between,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.IsNull) => QueryOperator.IsNull,
            _ when opSpan.EqualsOrdinalIgnoreCase(QuerySyntax.Operators.IsNotNull) => QueryOperator.IsNotNull,
            _ => default
        };

        return queryOperator != default;
    }
}