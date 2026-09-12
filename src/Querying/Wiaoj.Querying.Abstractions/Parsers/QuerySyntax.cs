namespace Wiaoj.Querying;

/// <summary>
/// Contains syntax tokens, delimiters, parameter prefixes, and operator string constants for query parsers.
/// </summary>
public static class QuerySyntax {
    /// <summary>
    /// The prefix character indicating the start of a query string ('?').
    /// </summary>
    public const char QueryStart = '?';

    /// <summary>
    /// The delimiter character separating query parameters ('&amp;').
    /// </summary>
    public const char ParameterDelimiter = '&';

    /// <summary>
    /// The delimiter character separating parameter keys and values ('=').
    /// </summary>
    public const char KeyValueDelimiter = '=';

    /// <summary>
    /// The opening bracket character for operator syntax ('[').
    /// </summary>
    public const char OpenBracket = '[';

    /// <summary>
    /// The closing bracket character for operator syntax (']').
    /// </summary>
    public const char CloseBracket = ']';

    /// <summary>
    /// The comma delimiter character for multi-value lists (',').
    /// </summary>
    public const char Comma = ',';

    /// <summary>
    /// The dot character used for nested navigation property paths ('.').
    /// </summary>
    public const char Dot = '.';

    /// <summary>
    /// The prefix character designating descending sort order ('-').
    /// </summary>
    public const char SortDescendingPrefix = '-';

    /// <summary>
    /// The prefix character designating ascending sort order ('+').
    /// </summary>
    public const char SortAscendingPrefix = '+';

    /// <summary>
    /// The delimiter string separating range bounds ("..").
    /// </summary>
    public const string RangeDelimiter = "..";

    /// <summary>
    /// Contains standard query string parameter names and prefixes.
    /// </summary>
    public static class Parameters {
        /// <summary>
        /// The query parameter name for free-text search ("q").
        /// </summary>
        public const string Q = "q";

        /// <summary>
        /// The query parameter prefix for free-text search ("q=").
        /// </summary>
        public const string QPrefix = "q=";

        /// <summary>
        /// The query parameter name for sort directives ("sort").
        /// </summary>
        public const string Sort = "sort";

        /// <summary>
        /// The query parameter prefix for sort directives ("sort=").
        /// </summary>
        public const string SortPrefix = "sort=";
    }

    /// <summary>
    /// Contains standard query operator string constants.
    /// </summary>
    public static class Operators {
        /// <summary>
        /// Equality comparison operator string ("eq").
        /// </summary>
        public const string Equal = "eq";

        /// <summary>
        /// Inequality comparison operator string ("neq").
        /// </summary>
        public const string NotEqual = "neq";

        /// <summary>
        /// Greater-than comparison operator string ("gt").
        /// </summary>
        public const string GreaterThan = "gt";

        /// <summary>
        /// Greater-than-or-equal comparison operator string ("gte").
        /// </summary>
        public const string GreaterThanOrEqual = "gte";

        /// <summary>
        /// Less-than comparison operator string ("lt").
        /// </summary>
        public const string LessThan = "lt";

        /// <summary>
        /// Less-than-or-equal comparison operator string ("lte").
        /// </summary>
        public const string LessThanOrEqual = "lte";

        /// <summary>
        /// Substring containment operator string ("contains").
        /// </summary>
        public const string Contains = "contains";

        /// <summary>
        /// Substring non-containment operator string ("notContains").
        /// </summary>
        public const string NotContains = "notContains";

        /// <summary>
        /// Prefix matching operator string ("startsWith").
        /// </summary>
        public const string StartsWith = "startsWith";

        /// <summary>
        /// Prefix non-matching operator string ("notStartsWith").
        /// </summary>
        public const string NotStartsWith = "notStartsWith";

        /// <summary>
        /// Suffix matching operator string ("endsWith").
        /// </summary>
        public const string EndsWith = "endsWith";

        /// <summary>
        /// Suffix non-matching operator string ("notEndsWith").
        /// </summary>
        public const string NotEndsWith = "notEndsWith";

        /// <summary>
        /// Set inclusion operator string ("in").
        /// </summary>
        public const string In = "in";

        /// <summary>
        /// Set exclusion operator string ("notIn").
        /// </summary>
        public const string NotIn = "notIn";

        /// <summary>
        /// Range boundary inclusion operator string ("between").
        /// </summary>
        public const string Between = "between";

        /// <summary>
        /// Range boundary exclusion operator string ("notBetween").
        /// </summary>
        public const string NotBetween = "notBetween";

        /// <summary>
        /// Null-check operator string ("isNull").
        /// </summary>
        public const string IsNull = "isNull";

        /// <summary>
        /// Not-null check operator string ("isNotNull").
        /// </summary>
        public const string IsNotNull = "isNotNull";
    }

    /// <summary>
    /// Gets the token callers write for <paramref name="queryOperator"/> — the one the parsers read.
    /// </summary>
    /// <remarks>
    /// The single mapping from operator to token. The OpenAPI transformer kept its own copy, which lacked
    /// <c>notBetween</c>, <c>isNull</c> and <c>isNotNull</c> and fell back to the enum name for them, so a
    /// generated document advertised <c>NotBetween</c> beside <c>between</c>.
    /// </remarks>
    public static string GetOperatorToken(QueryOperator queryOperator) {
        return queryOperator switch {
            QueryOperator.Equal => Operators.Equal,
            QueryOperator.NotEqual => Operators.NotEqual,
            QueryOperator.GreaterThan => Operators.GreaterThan,
            QueryOperator.GreaterThanOrEqual => Operators.GreaterThanOrEqual,
            QueryOperator.LessThan => Operators.LessThan,
            QueryOperator.LessThanOrEqual => Operators.LessThanOrEqual,
            QueryOperator.Contains => Operators.Contains,
            QueryOperator.NotContains => Operators.NotContains,
            QueryOperator.StartsWith => Operators.StartsWith,
            QueryOperator.NotStartsWith => Operators.NotStartsWith,
            QueryOperator.EndsWith => Operators.EndsWith,
            QueryOperator.NotEndsWith => Operators.NotEndsWith,
            QueryOperator.In => Operators.In,
            QueryOperator.NotIn => Operators.NotIn,
            QueryOperator.Between => Operators.Between,
            QueryOperator.NotBetween => Operators.NotBetween,
            QueryOperator.IsNull => Operators.IsNull,
            QueryOperator.IsNotNull => Operators.IsNotNull,
            _ => throw new ArgumentOutOfRangeException(nameof(queryOperator), queryOperator, "Unknown query operator.")
        };
    }
}
