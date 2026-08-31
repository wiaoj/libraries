using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Querying.JsonConverters;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying;

/// <summary>
/// Represents a single leaf filter condition node in the query AST.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(FilterConditionNodeJsonConverter))]
public readonly record struct FilterConditionNode :
    IEquatable<FilterConditionNode>,
    ISpanParsable<FilterConditionNode>,
    IUtf8SpanParsable<FilterConditionNode>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<FilterConditionNode, FilterConditionNode, bool> {

    private const int StackallocCharThreshold = 256;
    private const int MaxUtf8Length = 4096;

    /// <summary>
    /// Represents an empty or uninitialized <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static readonly FilterConditionNode Empty = default;

    /// <summary>
    /// Gets the target property or exposed field name.
    /// </summary>
    public string Field { get; init; }

    /// <summary>
    /// Gets the operator to apply.
    /// </summary>
    public QueryOperator Operator { get; init; }

    /// <summary>
    /// Gets the raw string value extracted from input.
    /// </summary>
    public string? RawValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether this node represents an empty or uninitialized state.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this.Field);

    /// <summary>
    /// Gets a value indicating whether the operator is unary (<see cref="QueryOperator.IsNull"/> or <see cref="QueryOperator.IsNotNull"/>).
    /// </summary>
    public bool IsUnary => this.Operator is QueryOperator.IsNull or QueryOperator.IsNotNull;

    /// <summary>
    /// Gets a value indicating whether a non-empty raw value is present.
    /// </summary>
    public bool HasValue => !string.IsNullOrEmpty(this.RawValue);

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterConditionNode"/> struct with default values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilterConditionNode() : this(string.Empty, QueryOperator.Equal, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterConditionNode"/> struct with an implicit equality operator.
    /// </summary>
    /// <param name="field">The target property or exposed field name.</param>
    /// <param name="rawValue">The raw string value extracted from input.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilterConditionNode(string field, string? rawValue) : this(field, QueryOperator.Equal, rawValue) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterConditionNode"/> struct with a field and unary operator.
    /// </summary>
    /// <param name="field">The target property or exposed field name.</param>
    /// <param name="op">The unary operator to apply.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilterConditionNode(string field, QueryOperator op) : this(field, op, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterConditionNode"/> struct.
    /// </summary>
    /// <param name="field">The target property or exposed field name.</param>
    /// <param name="op">The operator to apply.</param>
    /// <param name="rawValue">The raw string value extracted from input.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilterConditionNode(
        string field,
        QueryOperator op,
        string? rawValue) {
        this.Field = string.IsNullOrWhiteSpace(field) ? string.Empty : field.Trim();
        this.Operator = op;
        this.RawValue = (op is QueryOperator.IsNull or QueryOperator.IsNotNull) ? null : rawValue;
    }

    #region Static Fluent Factories

    /// <summary>Creates an equality filter condition.</summary>
    public static FilterConditionNode Equal(string field, object? value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        return new(field.Trim(), QueryOperator.Equal, FormatValue(value));
    }

    /// <summary>Creates a non-equality filter condition.</summary>
    public static FilterConditionNode NotEqual(string field, object? value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        return new(field.Trim(), QueryOperator.NotEqual, FormatValue(value));
    }

    /// <summary>Creates a greater-than filter condition.</summary>
    public static FilterConditionNode GreaterThan(string field, object value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.GreaterThan, FormatValue(value));
    }

    /// <summary>Creates a greater-than-or-equal filter condition.</summary>
    public static FilterConditionNode GreaterThanOrEqual(string field, object value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.GreaterThanOrEqual, FormatValue(value));
    }

    /// <summary>Creates a less-than filter condition.</summary>
    public static FilterConditionNode LessThan(string field, object value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.LessThan, FormatValue(value));
    }

    /// <summary>Creates a less-than-or-equal filter condition.</summary>
    public static FilterConditionNode LessThanOrEqual(string field, object value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.LessThanOrEqual, FormatValue(value));
    }

    /// <summary>Creates a substring contains filter condition.</summary>
    public static FilterConditionNode Contains(string field, string value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.Contains, value);
    }

    /// <summary>Creates a substring exclusion filter condition.</summary>
    public static FilterConditionNode NotContains(string field, string value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.NotContains, value);
    }

    /// <summary>Creates a prefix starts-with filter condition.</summary>
    public static FilterConditionNode StartsWith(string field, string value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.StartsWith, value);
    }

    /// <summary>Creates a prefix exclusion filter condition.</summary>
    public static FilterConditionNode NotStartsWith(string field, string value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.NotStartsWith, value);
    }

    /// <summary>Creates a suffix ends-with filter condition.</summary>
    public static FilterConditionNode EndsWith(string field, string value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.EndsWith, value);
    }

    /// <summary>Creates a suffix exclusion filter condition.</summary>
    public static FilterConditionNode NotEndsWith(string field, string value) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(value);
        return new(field.Trim(), QueryOperator.NotEndsWith, value);
    }

    /// <summary>Creates a collection inclusion filter condition.</summary>
    public static FilterConditionNode In(string field, string rawCommaSeparated) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(rawCommaSeparated);
        return new(field.Trim(), QueryOperator.In, rawCommaSeparated);
    }

    /// <summary>Creates a collection exclusion filter condition.</summary>
    public static FilterConditionNode NotIn(string field, string rawCommaSeparated) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(rawCommaSeparated);
        return new(field.Trim(), QueryOperator.NotIn, rawCommaSeparated);
    }

    /// <summary>Creates an inclusive boundary range filter condition.</summary>
    public static FilterConditionNode Between(string field, object lower, object upper) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(lower);
        Preca.ThrowIfNull(upper);
        return new(field.Trim(), QueryOperator.Between, $"{FormatValue(lower)}{QuerySyntax.RangeDelimiter}{FormatValue(upper)}");
    }

    /// <summary>Creates an exclusion boundary range filter condition.</summary>
    public static FilterConditionNode NotBetween(string field, object lower, object upper) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        Preca.ThrowIfNull(lower);
        Preca.ThrowIfNull(upper);
        return new(field.Trim(), QueryOperator.NotBetween, $"{FormatValue(lower)}{QuerySyntax.RangeDelimiter}{FormatValue(upper)}");
    }

    /// <summary>Creates a null-check filter condition.</summary>
    public static FilterConditionNode IsNull(string field) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        return new(field.Trim(), QueryOperator.IsNull, null);
    }

    /// <summary>Creates a not-null filter condition.</summary>
    public static FilterConditionNode IsNotNull(string field) {
        Preca.ThrowIfEmptyOrWhiteSpace(field);
        return new(field.Trim(), QueryOperator.IsNotNull, null);
    }

    private static string FormatValue(object? value) {
        return value is null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    #endregion

    #region Parsing (Public API)

    /// <summary>
    /// Parses a bracket-style parameter string into a <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static FilterConditionNode Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static FilterConditionNode Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out FilterConditionNode result)) {
            return result;
        }

        throw new FormatException("Invalid bracket query parameter format.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static FilterConditionNode Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out FilterConditionNode result)) {
            return result;
        }

        throw new FormatException("Invalid UTF-8 byte sequence for FilterConditionNode.");
    }

    /// <summary>
    /// Attempts to parse a parameter string into a <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out FilterConditionNode result) {
        if(s is null) {
            result = Empty;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a character span into a <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out FilterConditionNode result) {
        return BracketQueryParser.TryParse(s, out result);
    }

    /// <summary>
    /// Attempts to parse a UTF-8 byte span into a <see cref="FilterConditionNode"/> instance.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out FilterConditionNode result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return false;
        }

        if(utf8Text.Length > MaxUtf8Length) {
            result = Empty;
            return false;
        }

        using ValueBuffer<char> buffer = new(utf8Text.Length, stackalloc char[StackallocCharThreshold]);
        Span<char> charBuffer = buffer.Span;

        if(Utf8.ToUtf16(utf8Text, charBuffer, out _, out int charsWritten, replaceInvalidSequences: false) == OperationStatus.Done) {
            return TryParse(charBuffer[..charsWritten], out result);
        }

        result = Empty;
        return false;
    }

    #endregion

    #region Formatting (Public API)

    /// <inheritdoc/>
    public override string ToString() {
        if(this.IsEmpty) {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[128];
        if(TryFormat(buffer, out int charsWritten)) {
            return buffer[..charsWritten].ToString();
        }

        int requiredLength = this.Field.Length + 20 + (this.RawValue?.Length ?? 0);
        using ValueBuffer<char> pooledBuffer = new(requiredLength, stackalloc char[256]);
        if(TryFormat(pooledBuffer.Span, out int pooledWritten)) {
            return pooledBuffer.Span[..pooledWritten].ToString();
        }

        return string.Empty;
    }

    /// <summary>
    /// Formats the filter condition into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character buffer.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        charsWritten = 0;
        if(this.IsEmpty) {
            return true;
        }

        ReadOnlySpan<char> fieldSpan = this.Field.AsSpan();
        ReadOnlySpan<char> opSpan = GetOperatorString(this.Operator).AsSpan();
        ReadOnlySpan<char> valSpan = this.RawValue.AsSpan();

        int requiredLength = fieldSpan.Length + 1 + opSpan.Length + 1;
        if(!this.IsUnary) {
            requiredLength += 1 + valSpan.Length;
        }

        if(destination.Length < requiredLength) {
            return false;
        }

        int offset = 0;
        fieldSpan.CopyTo(destination[offset..]);
        offset += fieldSpan.Length;

        destination[offset++] = QuerySyntax.OpenBracket;
        opSpan.CopyTo(destination[offset..]);
        offset += opSpan.Length;
        destination[offset++] = QuerySyntax.CloseBracket;

        if(!this.IsUnary) {
            destination[offset++] = QuerySyntax.KeyValueDelimiter;
            valSpan.CopyTo(destination[offset..]);
            offset += valSpan.Length;
        }

        charsWritten = offset;
        return true;
    }

    /// <summary>
    /// Formats the filter condition into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte buffer.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        bytesWritten = 0;
        if(this.IsEmpty) {
            return true;
        }

        Span<char> charBuf = stackalloc char[256];
        if(!TryFormat(charBuf, out int charsWritten)) {
            return false;
        }

        return Utf8.FromUtf16(charBuf[..charsWritten], utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    private static string GetOperatorString(QueryOperator op) {
        return op switch {
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
            _ => string.Empty
        };
    }

    #endregion

    #region Explicit Interface Implementations

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return ToString();
    }

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(utf8Destination, out bytesWritten);
    }

    static FilterConditionNode IParsable<FilterConditionNode>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<FilterConditionNode>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FilterConditionNode result) {
        return TryParse(s, out result);
    }

    static FilterConditionNode ISpanParsable<FilterConditionNode>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<FilterConditionNode>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FilterConditionNode result) {
        return TryParse(s, out result);
    }

    static FilterConditionNode IUtf8SpanParsable<FilterConditionNode>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<FilterConditionNode>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out FilterConditionNode result) {
        return TryParse(utf8Text, out result);
    }

    #endregion
}