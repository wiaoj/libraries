using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Wiaoj.Pagination.JsonConverters;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents an immutable, opaque pagination cursor guaranteed to be a valid Base64Url string (RFC 4648, Section 5).
/// </summary>
/// <remarks>
/// <para>
/// Eliminates primitive obsession around cursor strings, providing zero-allocation decoding, SIMD-accelerated validation,
/// and support for .NET 10 alternate span lookups in collections.
/// </para>
/// </remarks>
[DebuggerDisplay("{Value,nq}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(CursorTokenJsonConverter))]
public readonly record struct CursorToken :
    IEquatable<CursorToken>,
    IComparable<CursorToken>,
    IComparable,
    ISpanParsable<CursorToken>,
    IUtf8SpanParsable<CursorToken>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<CursorToken, CursorToken, bool>,
    IComparisonOperators<CursorToken, CursorToken, bool> {

    private readonly Base64UrlString _value;

    /// <summary>
    /// Represents an empty or uninitialized cursor token.
    /// </summary>
    public static readonly CursorToken Empty = default;

    /// <summary>
    /// Gets a value indicating whether this cursor token is empty or uninitialized.
    /// </summary>
    public bool IsEmpty => this._value.IsEmpty;

    /// <summary>
    /// Gets the length of the cursor token string.
    /// </summary>
    public int Length => this._value.Length;

    /// <summary>
    /// Gets the underlying Base64Url-encoded string representation.
    /// </summary>
    public string Value => this._value.Value;

    private CursorToken(Base64UrlString value) {
        this._value = value;
    }

    #region Factories

    /// <summary>
    /// Creates a <see cref="CursorToken"/> by encoding raw binary payload bytes.
    /// </summary>
    /// <param name="bytes">The raw bytes to encode.</param>
    /// <returns>A new <see cref="CursorToken"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CursorToken FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) {
            return Empty;
        }
        return new CursorToken(Base64UrlString.FromBytes(bytes));
    }

    /// <summary>
    /// Creates a <see cref="CursorToken"/> by encoding a UTF-8 text payload.
    /// </summary>
    /// <param name="text">The string to encode.</param>
    /// <returns>A new <see cref="CursorToken"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CursorToken FromUtf8(string text) {
        if(string.IsNullOrEmpty(text)) {
            return Empty;
        }
        return new CursorToken(Base64UrlString.FromUtf8(text));
    }

    /// <summary>
    /// Creates a <see cref="CursorToken"/> by encoding a UTF-8 byte span payload.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 bytes to encode.</param>
    /// <returns>A new <see cref="CursorToken"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CursorToken FromUtf8(ReadOnlySpan<byte> utf8Text) {
        if(utf8Text.IsEmpty) {
            return Empty;
        }
        return new CursorToken(Base64UrlString.FromUtf8(utf8Text));
    }

    #endregion

    #region Decoding

    /// <summary>
    /// Decodes the underlying binary cursor payload into the destination byte span without heap allocations.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if decoding succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        if(this._value.TryDecode(destination, out bytesWritten)) {
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    /// <summary>
    /// Gets the exact number of bytes that the decoded payload represents.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetDecodedLength() {
        return this._value.GetDecodedLength();
    }

    #endregion

    #region Parsing (ISpanParsable, IUtf8SpanParsable)

    /// <summary>
    /// Parses a string into a <see cref="CursorToken"/>.
    /// </summary>
    public static CursorToken Parse(string s) {
        Preca.ThrowIfNull(s);
        return new CursorToken(Base64UrlString.Parse(s));
    }

    /// <summary>
    /// Parses a character span into a <see cref="CursorToken"/>.
    /// </summary>
    public static CursorToken Parse(ReadOnlySpan<char> s) {
        return new CursorToken(Base64UrlString.Parse(s));
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="CursorToken"/>.
    /// </summary>
    public static CursorToken Parse(ReadOnlySpan<byte> utf8Text) {
        return new CursorToken(Base64UrlString.Parse(utf8Text));
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="CursorToken"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out CursorToken result) {
        if(Base64UrlString.TryParse(s, out Base64UrlString parsed)) {
            result = new CursorToken(parsed);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="CursorToken"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out CursorToken result) {
        if(Base64UrlString.TryParse(s, out Base64UrlString parsed)) {
            result = new CursorToken(parsed);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="CursorToken"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out CursorToken result) {
        if(Base64UrlString.TryParse(utf8Text, out Base64UrlString parsed)) {
            result = new CursorToken(parsed);
            return true;
        }
        result = default;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <summary>
    /// Tries to format the cursor into the destination character span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        return this._value.TryFormat(destination, out charsWritten);
    }

    /// <summary>
    /// Tries to format the cursor into the destination UTF-8 byte span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return this._value.TryFormat(utf8Destination, out bytesWritten);
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
        return this._value.TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return this._value.TryFormat(utf8Destination, out bytesWritten);
    }

    static CursorToken IParsable<CursorToken>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<CursorToken>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CursorToken result) {
        return TryParse(s, out result);
    }

    static CursorToken ISpanParsable<CursorToken>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<CursorToken>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CursorToken result) {
        return TryParse(s, out result);
    }

    static CursorToken IUtf8SpanParsable<CursorToken>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<CursorToken>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CursorToken result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region Comparison & Ordering

    /// <inheritdoc/>
    public int CompareTo(CursorToken other) {
        return this._value.CompareTo(other._value);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is CursorToken other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(CursorToken)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(CursorToken left, CursorToken right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(CursorToken left, CursorToken right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(CursorToken left, CursorToken right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(CursorToken left, CursorToken right) {
        return left.CompareTo(right) >= 0;
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="CursorToken"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/> and <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    public static IEqualityComparer<CursorToken> OrdinalComparer => CursorTokenOrdinalComparer.Instance;

    private sealed class CursorTokenOrdinalComparer :
        IEqualityComparer<CursorToken>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, CursorToken>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, CursorToken> {

        public static CursorTokenOrdinalComparer Instance { get; } = new();

        public bool Equals(CursorToken x, CursorToken y) {
            return x.Equals(y);
        }

        public int GetHashCode(CursorToken obj) {
            return obj.GetHashCode();
        }

        // Char Span
        public bool Equals(ReadOnlySpan<char> alternate, CursorToken other) {
            return alternate.SequenceEqual(other.Value.AsSpan());
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            return string.GetHashCode(alternate, StringComparison.Ordinal);
        }

        public CursorToken Create(ReadOnlySpan<char> alternate) {
            return CursorToken.Parse(alternate);
        }

        // UTF-8 Byte Span
        public bool Equals(ReadOnlySpan<byte> alternate, CursorToken other) {
            if(alternate.Length != other.Length) return false;

            Span<char> charBuf = stackalloc char[alternate.Length];
            if(System.Text.Ascii.ToUtf16(alternate, charBuf, out _) == OperationStatus.Done) {
                return charBuf.SequenceEqual(other.Value.AsSpan());
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<byte> alternate) {
            Span<char> charBuf = stackalloc char[alternate.Length];
            if(System.Text.Ascii.ToUtf16(alternate, charBuf, out _) == OperationStatus.Done) {
                return string.GetHashCode(charBuf, StringComparison.Ordinal);
            }
            return 0;
        }

        public CursorToken Create(ReadOnlySpan<byte> alternate) {
            return CursorToken.Parse(alternate);
        }
    }

    #endregion

    #region Implicit / Explicit Operators

    /// <summary>
    /// Implicitly converts a <see cref="CursorToken"/> to its underlying string value.
    /// </summary>
    /// <param name="token">The cursor token instance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(CursorToken token) {
        return token.Value;
    }

    /// <summary>
    /// Implicitly converts a <see cref="CursorToken"/> to a character span.
    /// </summary>
    /// <param name="token">The cursor token instance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(CursorToken token) {
        return token.Value.AsSpan();
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="CursorToken"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <exception cref="FormatException">Thrown if the string is not valid Base64Url.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator CursorToken(string s) {
        return Parse(s);
    }

    #endregion
}