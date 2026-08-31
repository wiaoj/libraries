using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Querying;
/// <summary>
/// Represents a normalized free-text query search term (<c>q=term</c>).
/// </summary>
/// <remarks>
/// Trims and null-normalizes the input on construction. Equality and ordering use
/// <see cref="StringComparison.OrdinalIgnoreCase"/>, so two terms differing only by case are
/// treated as equal in <see cref="HashSet{T}"/>, <see cref="SortedSet{T}"/>, <c>Distinct()</c>,
/// and <c>OrderBy()</c>.
/// </remarks>
[DebuggerDisplay("{Value,nq}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct Q :
    IEquatable<Q>,
    IComparable<Q>,
    IComparable,
    ISpanParsable<Q>,
    IUtf8SpanParsable<Q>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<Q, Q, bool>,
    IComparisonOperators<Q, Q, bool> {
    /// <summary>
    /// Maximum number of UTF-8 bytes accepted by the UTF-8 parsing overloads.
    /// </summary>
    public const int MaxUtf8Length = 4096;

    /// <summary>
    /// Size of the stack-allocated fast-path buffer used while decoding UTF-8 input.
    /// Inputs whose decoded length exceeds this fall back to pooled memory instead of growing the stack allocation.
    /// </summary>
    private const int StackallocCharThreshold = 256;

    private readonly string? _value;

    /// <summary>
    /// An empty or uninitialized <see cref="Q"/> instance.
    /// </summary>
    public static readonly Q Empty = default;

    /// <summary>
    /// Gets a value indicating whether this term is empty or consists only of white-space characters.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    /// <summary>
    /// Gets the number of characters in the normalized term.
    /// </summary>
    public int Length => this._value?.Length ?? 0;

    /// <summary>
    /// Gets the underlying normalized string value. Returns an empty string if uninitialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="Q"/> struct.
    /// </summary>
    /// <param name="value">The raw input search string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Q(string? value) {
        this._value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Q"/> struct from a character span.
    /// </summary>
    /// <param name="value">The raw input character span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Q(ReadOnlySpan<char> value) {
        ReadOnlySpan<char> trimmed = value.Trim();
        this._value = trimmed.IsEmpty ? null : trimmed.ToString();
    }

    #region Parsing (ISpanParsable, IUtf8SpanParsable)

    /// <summary>
    /// Parses a string into a <see cref="Q"/> instance.
    /// </summary>
    public static Q Parse(string s) {
        return new(s);
    }

    /// <summary>
    /// Parses a character span into a <see cref="Q"/> instance.
    /// </summary>
    public static Q Parse(ReadOnlySpan<char> s) {
        return new(s);
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Q"/> instance.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="utf8Text"/> exceeds <see cref="MaxUtf8Length"/> or contains
    /// a malformed UTF-8 sequence, consistent with the throwing contract of
    /// <see cref="IUtf8SpanParsable{TSelf}.Parse(ReadOnlySpan{byte}, IFormatProvider?)"/>.
    /// </exception>
    public static Q Parse(ReadOnlySpan<byte> utf8Text) {
        if(utf8Text.IsEmpty) {
            return Empty;
        }

        if(utf8Text.Length > MaxUtf8Length) {
            throw new ArgumentException(
                $"UTF-8 payload exceeds the maximum allowed length of {MaxUtf8Length} bytes.",
                nameof(utf8Text));
        }

        using ValueBuffer<char> buffer = new(utf8Text.Length, stackalloc char[StackallocCharThreshold]);
        var charBuffer = buffer.Span;
        if(Utf8.ToUtf16(utf8Text, charBuffer, out _, out var charsWritten, replaceInvalidSequences: false) == OperationStatus.Done) {
            return new Q(charBuffer[..charsWritten]);
        }

        throw new ArgumentException("The input contains an invalid UTF-8 byte sequence.", nameof(utf8Text));
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Q"/> instance.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Q result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = Empty;
            return true;
        }

        result = new Q(s);
        return true;
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Q"/> instance.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Q result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            result = Empty;
            return true;
        }

        result = new Q(trimmed);
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Q"/> instance.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if <paramref name="utf8Text"/> was empty, within
    /// <see cref="MaxUtf8Length"/>, and valid UTF-8; otherwise <see langword="false"/>.
    /// Unlike <see cref="Parse(ReadOnlySpan{byte})"/>, this overload never throws.
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Q result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        if(utf8Text.Length > MaxUtf8Length) {
            result = Empty;
            return false;
        }

        using ValueBuffer<char> buffer = new(utf8Text.Length, stackalloc char[StackallocCharThreshold]);
        var charBuffer = buffer.Span;
        if(Utf8.ToUtf16(utf8Text, charBuffer, out _, out var charsWritten, replaceInvalidSequences: false) == OperationStatus.Done) {
            result = new Q(charBuffer[..charsWritten]);
            return true;
        }

        result = Empty;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <summary>
    /// Returns the normalized term as a character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> AsSpan() {
        return this.Value.AsSpan();
    }

    /// <summary>
    /// Tries to format the search term into the destination character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            charsWritten = 0;
            return true;
        }

        ReadOnlySpan<char> source = this.Value.AsSpan();
        if(destination.Length < source.Length) {
            charsWritten = 0;
            return false;
        }

        source.CopyTo(destination);
        charsWritten = source.Length;
        return true;
    }

    /// <summary>
    /// Tries to format the search term into the destination UTF-8 byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            bytesWritten = 0;
            return true;
        }

        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    // --- Explicit Interface Implementations ---

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
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

    static Q IParsable<Q>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<Q>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Q result) {
        return TryParse(s, out result);
    }

    static Q ISpanParsable<Q>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<Q>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Q result) {
        return TryParse(s, out result);
    }

    static Q IUtf8SpanParsable<Q>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<Q>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Q result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region Equality (case-insensitive, consistent with CompareTo)

    /// <summary>
    /// Determines whether this instance equals <paramref name="other"/> using an ordinal,
    /// case-insensitive comparison of <see cref="Value"/>.
    /// </summary>
    /// <remarks>
    /// Overrides the compiler-synthesized <c>record struct</c> equality (which would otherwise
    /// compare the backing field with ordinal, case-sensitive semantics) so that
    /// <see cref="Equals(Q)"/> stays consistent with <see cref="CompareTo(Q)"/>.
    /// </remarks>
    public bool Equals(Q other) {
        return string.Equals(this.Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return string.GetHashCode(this.Value, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Comparison & Ordering

    /// <inheritdoc/>
    public int CompareTo(Q other) {
        return string.Compare(this.Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Q other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Q)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Q left, Q right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Q left, Q right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Q left, Q right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Q left, Q right) {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}