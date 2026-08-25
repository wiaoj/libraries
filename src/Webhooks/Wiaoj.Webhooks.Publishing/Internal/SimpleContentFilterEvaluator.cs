using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// High-performance content filter evaluator that parses, tokenizes, and evaluates comparison expressions with cached reflection getters.
/// </summary>
internal sealed class SimpleContentFilterEvaluator : IWebhookContentFilterEvaluator {
    private static readonly string[] Operators = [">=", "<=", "!=", "==", ">", "<"];
    private static readonly string[] ConjunctionSeparators = ["&&", "AND", "and"];

    private readonly ConcurrentDictionary<string, ParsedClause[]?> _expressionCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo?> _propertyCache = new();

    /// <inheritdoc/>
    public bool Evaluate<TPayload>(string? filterExpression, TPayload payload) {
        if(string.IsNullOrWhiteSpace(filterExpression)) {
            return true;
        }

        Preca.ThrowIfNull(payload);

        ParsedClause[]? clauses = this._expressionCache.GetOrAdd(filterExpression, static expr => ParseExpression(expr));
        if(clauses is null || clauses.Length == 0) {
            return false;
        }

        Type payloadType = payload.GetType();

        for(int i = 0; i < clauses.Length; i++) {
            ref readonly ParsedClause clause = ref clauses[i];

            PropertyInfo? property = this._propertyCache.GetOrAdd(
                (payloadType, clause.PropertyName),
                static key => key.Type.GetProperty(key.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));

            if(property is null) {
                return false;
            }

            object? actualValue = property.GetValue(payload);
            if(!EvaluateClause(in clause, actualValue)) {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluateClause(in ParsedClause clause, object? actualValue) {
        bool targetIsNull = string.Equals(clause.RawTargetValue, "null", StringComparison.OrdinalIgnoreCase);

        if(actualValue is null) {
            return clause.Operator switch {
                ComparisonOperator.Equal => targetIsNull,
                ComparisonOperator.NotEqual => !targetIsNull,
                _ => false // Invalid comparisons like null > 5 -> false
            };
        }

        if(targetIsNull) {
            return clause.Operator switch {
                ComparisonOperator.Equal => false,
                ComparisonOperator.NotEqual => true,
                _ => false
            };
        }

        if(clause.IsNumeric && TryConvertToDecimal(actualValue, out decimal actualNumeric)) {
            return clause.Operator switch {
                ComparisonOperator.Equal => actualNumeric == clause.NumericTargetValue,
                ComparisonOperator.NotEqual => actualNumeric != clause.NumericTargetValue,
                ComparisonOperator.GreaterThan => actualNumeric > clause.NumericTargetValue,
                ComparisonOperator.GreaterThanOrEqual => actualNumeric >= clause.NumericTargetValue,
                ComparisonOperator.LessThan => actualNumeric < clause.NumericTargetValue,
                ComparisonOperator.LessThanOrEqual => actualNumeric <= clause.NumericTargetValue,
                _ => false
            };
        }

        string actualStr = actualValue.ToString() ?? string.Empty;
        int stringComparison = string.Compare(actualStr, clause.RawTargetValue, StringComparison.OrdinalIgnoreCase);

        return clause.Operator switch {
            ComparisonOperator.Equal => stringComparison == 0,
            ComparisonOperator.NotEqual => stringComparison != 0,
            _ => false
        };
    }

    private static bool TryConvertToDecimal(object value, out decimal result) {
        if(value is decimal d) {
            result = d;
            return true;
        }
        if(value is int i) {
            result = i;
            return true;
        }
        if(value is long l) {
            result = l;
            return true;
        }
        if(value is double db) {
            result = (decimal)db;
            return true;
        }
        if(value is float f) {
            result = (decimal)f;
            return true;
        }

        return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static ParsedClause[]? ParseExpression(string expression) {
        ReadOnlySpan<char> trimmed = expression.AsSpan().Trim();
        if(trimmed.IsEmpty) {
            return null;
        }

        // Reject dangling or leading logical operators
        if(trimmed.StartsWith("&&".AsSpan(), StringComparison.Ordinal) ||
           trimmed.EndsWith("&&".AsSpan(), StringComparison.Ordinal) ||
           trimmed.StartsWith("AND".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
           trimmed.EndsWith("AND".AsSpan(), StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        string[] rawClauses = expression.Split(ConjunctionSeparators, StringSplitOptions.None);
        if(rawClauses.Length == 0) {
            return null;
        }

        ParsedClause[] clauses = new ParsedClause[rawClauses.Length];

        for(int i = 0; i < rawClauses.Length; i++) {
            if(!TryParseClause(rawClauses[i], out ParsedClause clause)) {
                return null;
            }
            clauses[i] = clause;
        }

        return clauses;
    }

    private static bool TryParseClause(string rawClause, out ParsedClause clause) {
        clause = default;
        ReadOnlySpan<char> span = rawClause.AsSpan().Trim();
        if(span.IsEmpty) {
            return false;
        }

        string? matchedOp = null;
        int opIndex = -1;

        for(int i = 0; i < Operators.Length; i++) {
            opIndex = span.IndexOf(Operators[i].AsSpan(), StringComparison.Ordinal);
            if(opIndex >= 0) {
                matchedOp = Operators[i];
                break;
            }
        }

        if(matchedOp is null || opIndex <= 0) {
            return false;
        }

        ReadOnlySpan<char> propSpan = span[..opIndex].Trim();
        ReadOnlySpan<char> rawValueSpan = span[(opIndex + matchedOp.Length)..].Trim();

        if(propSpan.IsEmpty || rawValueSpan.IsEmpty) {
            return false;
        }

        // Property name must be a valid C# identifier
        if(!IsValidIdentifier(propSpan)) {
            return false;
        }

        // Right-hand value cannot start with comparison operator remnants (e.g. ===, >= <=)
        if(rawValueSpan.StartsWith("=".AsSpan(), StringComparison.Ordinal) ||
           rawValueSpan.StartsWith(">".AsSpan(), StringComparison.Ordinal) ||
           rawValueSpan.StartsWith("<".AsSpan(), StringComparison.Ordinal) ||
           rawValueSpan.StartsWith("!".AsSpan(), StringComparison.Ordinal)) {
            return false;
        }

        string propName = propSpan.ToString();
        string rawValue = rawValueSpan.ToString();

        if(rawValue.Length >= 2 && ((rawValue.StartsWith('\'') && rawValue.EndsWith('\'')) || (rawValue.StartsWith('"') && rawValue.EndsWith('"')))) {
            rawValue = rawValue[1..^1];
        }

        ComparisonOperator op = matchedOp switch {
            "==" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "<=" => ComparisonOperator.LessThanOrEqual,
            ">" => ComparisonOperator.GreaterThan,
            "<" => ComparisonOperator.LessThan,
            _ => ComparisonOperator.Equal
        };

        bool isNumeric = decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal numVal);

        clause = new ParsedClause(propName, op, rawValue, isNumeric, isNumeric ? numVal : 0);
        return true;
    }

    private static bool IsValidIdentifier(ReadOnlySpan<char> span) {
        if(span.IsEmpty) {
            return false;
        }

        if(!char.IsLetter(span[0]) && span[0] != '_') {
            return false;
        }

        for(int i = 1; i < span.Length; i++) {
            if(!char.IsLetterOrDigit(span[i]) && span[i] != '_') {
                return false;
            }
        }

        return true;
    }

    private enum ComparisonOperator {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual
    }

    private readonly record struct ParsedClause(
        string PropertyName,
        ComparisonOperator Operator,
        string RawTargetValue,
        bool IsNumeric,
        decimal NumericTargetValue);
}