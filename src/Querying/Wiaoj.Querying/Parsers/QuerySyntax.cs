namespace Wiaoj.Querying.Parsers;

/// <summary>
/// Contains syntax tokens, delimiters, parameter prefixes, and operator string constants for query parsers.
/// </summary>
internal static class QuerySyntax {
    public const char QueryStart = '?';
    public const char ParameterDelimiter = '&';
    public const char KeyValueDelimiter = '=';
    public const char OpenBracket = '[';
    public const char CloseBracket = ']';
    public const char Comma = ',';
    public const char Dot = '.';
    public const char SortDescendingPrefix = '-';
    public const char SortAscendingPrefix = '+';
    public const string RangeDelimiter = "..";

    public static class Parameters {
        public const string Q = "q";
        public const string QPrefix = "q=";
        public const string Sort = "sort";
        public const string SortPrefix = "sort=";
    }

    public static class Operators {
        public const string Equal = "eq";
        public const string NotEqual = "neq";
        public const string GreaterThan = "gt";
        public const string GreaterThanOrEqual = "gte";
        public const string LessThan = "lt";
        public const string LessThanOrEqual = "lte";
        public const string Contains = "contains";
        public const string NotContains = "notContains";
        public const string StartsWith = "startsWith";
        public const string NotStartsWith = "notStartsWith";
        public const string EndsWith = "endsWith";
        public const string NotEndsWith = "notEndsWith";
        public const string In = "in";
        public const string NotIn = "notIn";
        public const string Between = "between";
        public const string NotBetween = "notBetween";
        public const string IsNull = "isNull";
        public const string IsNotNull = "isNotNull";
    }
}