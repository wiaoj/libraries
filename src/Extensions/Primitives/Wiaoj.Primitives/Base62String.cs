using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives; 
/// <summary>
/// Represents a structurally valid Base62 string (alphanumeric [0-9a-zA-Z]).
/// </summary>
/// <remarks>
/// Base62 is commonly used for URL shortening and compact representation of large integers
/// (like UUIDs or Snowflake IDs). This implementation uses Big Endian byte order for byte-array encoding.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(Base62StringJsonConverter))]
public readonly record struct Base62String :
    IEquatable<Base62String>,
    IComparable<Base62String>,
    IComparable,
    ISpanParsable<Base62String>,
    IUtf8SpanParsable<Base62String>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<Base62String, Base62String, bool> {

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly SearchValues<char> Base62Chars =
        SearchValues.Create(Alphabet);

    private static readonly SearchValues<byte> Base62Utf8Bytes =
        SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    private readonly string _value;

    /// <summary>Gets an instance representing an empty Base62 string.</summary>
    public static Base62String Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the underlying Base62 string value.
    /// Returns an empty string if the structure is default.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    private Base62String(string value) {
        this._value = value;
    }

    #region Creation

    /// <summary>
    /// Encodes a generic byte span (Big Endian) into a <see cref="Base62String"/>.
    /// Suitable for converting UUIDs or arbitrary large numbers into a compact format.
    /// </summary>
    /// <param name="bytes">The byte span to encode.</param>
    /// <returns>A valid Base62 string representation of the byte array.</returns>
    [SkipLocalsInit]
    public static Base62String FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) return Empty;

        int estimatedLength = (int)Math.Ceiling(bytes.Length * 8.0 / 5.954196);
        BigInteger bigInt = new(bytes, isUnsigned: true, isBigEndian: true);

        return new Base62String(string.Create(estimatedLength, bigInt, (span, number) => {
            int i = span.Length - 1;

            if(number.IsZero) {
                span[0] = Alphabet[0];
                return;
            }

            BigInteger base62 = new(62);
            while(number > BigInteger.Zero && i >= 0) {
                (number, BigInteger remainder) = BigInteger.DivRem(number, base62);
                span[i--] = Alphabet[(int)remainder];
            }

            while(i >= 0)
                span[i--] = Alphabet[0];
        }));
    }

    /// <summary>
    /// Encodes a 64-bit signed integer (long) into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="value">The non-negative integer to encode.</param>
    /// <returns>A valid Base62 string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is negative.</exception>
    [SkipLocalsInit]
    public static Base62String FromInt64(long value) {
        Preca.ThrowIfNegative(value);
        if(value == 0) return new Base62String("0");

        // long.MaxValue in Base62 is "AzL8n0Y58m7" (11 digits). 13 for safety.
        Span<char> buffer = stackalloc char[13];
        int idx = 12;

        while(value > 0) {
            buffer[idx--] = Alphabet[(int)(value % 62)];
            value /= 62;
        }

        return new Base62String(buffer[(idx + 1)..].ToString());
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A valid <see cref="Base62String"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 characters.</exception>
    public static Base62String Parse(string s) {
        Preca.ThrowIfNull(s);
        if(TryParse(s.AsSpan(), out Base62String result)) return result;
        throw new FormatException($"Invalid Base62 string: '{s}'");
    }

    /// <summary>
    /// Parses a character span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="Base62String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 characters.</exception>
    public static Base62String Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out Base62String result)) return result;
        throw new FormatException("Invalid Base62 string.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A valid <see cref="Base62String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 UTF-8 sequence.</exception>
    public static Base62String Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out Base62String result)) return result;
        throw new FormatException("Invalid Base62 UTF-8 sequence.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base62String result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out Base62String result) {
        if(s.IsEmpty) { result = Empty; return true; }
        if(s.IndexOfAnyExcept(Base62Chars) >= 0) { result = default; return false; }
        result = new Base62String(s.ToString());
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base62String result) {
        if(utf8Text.IsEmpty) { result = Empty; return true; }
        if(utf8Text.IndexOfAnyExcept(Base62Utf8Bytes) >= 0) { result = default; return false; }
        result = new Base62String(Encoding.UTF8.GetString(utf8Text));
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Base62String IParsable<Base62String>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Base62String>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base62String result) => TryParse(s, out result);
    static Base62String ISpanParsable<Base62String>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Base62String>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base62String result) => TryParse(s, out result);
    static Base62String IUtf8SpanParsable<Base62String>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Base62String>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base62String result) => TryParse(utf8Text, out result);

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <summary>
    /// Formats the Base62 string.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <returns>The Base62 string value.</returns>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the Base62 string using the specified format provider.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns>The Base62 string value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

    /// <summary>
    /// Tries to format the Base62 string into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the Base62 string into the destination character span using the specified format.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the Base62 string into the destination character span using the specified format and provider.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <param name="provider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <summary>
    /// Tries to format the Base62 string into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the Base62 string into the destination UTF-8 byte span using the specified format.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the Base62 string into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <param name="provider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Destination);
        return true;
    }

    #endregion

    #region Comparison & Ordering

    /// <summary>
    /// Compares the current instance with another <see cref="Base62String"/> using ordinal comparison.
    /// </summary>
    /// <param name="other">The other <see cref="Base62String"/> to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(Base62String other) => string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares the current instance with another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="Base62String"/>.</exception>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Base62String other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Base62String)}", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Base62String left, Base62String right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Base62String left, Base62String right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Base62String left, Base62String right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Base62String left, Base62String right) => left.CompareTo(right) >= 0;

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Base62String"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base62String> OrdinalComparer => Base62StringOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Base62String"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base62String> OrdinalIgnoreCaseComparer => Base62StringOrdinalIgnoreCaseComparer.Instance;

    private sealed class Base62StringOrdinalComparer : IEqualityComparer<Base62String>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base62String> {
        public static Base62StringOrdinalComparer Instance { get; } = new();

        public bool Equals(Base62String x, Base62String y) => string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(Base62String obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.Ordinal);

        public bool Equals(ReadOnlySpan<char> alternate, Base62String other) => alternate.SequenceEqual(other.Value.AsSpan());

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.Ordinal);

        public Base62String Create(ReadOnlySpan<char> alternate) => Base62String.Parse(alternate);
    }

    private sealed class Base62StringOrdinalIgnoreCaseComparer : IEqualityComparer<Base62String>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base62String> {
        public static Base62StringOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Base62String x, Base62String y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Base62String obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public bool Equals(ReadOnlySpan<char> alternate, Base62String other) => MemoryExtensions.Equals(alternate, other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public Base62String Create(ReadOnlySpan<char> alternate) => Base62String.Parse(alternate);
    }

    #endregion

    #region Decoding

    /// <summary>
    /// Decodes the Base62 string back to a <see cref="long"/>.
    /// Uses Horner's Method for high performance and accurate overflow detection.
    /// </summary>
    /// <returns>The decoded 64-bit integer.</returns>
    public long ToInt64() {
        if(string.IsNullOrEmpty(this._value)) return 0;

        long result = 0;
        foreach(char c in this._value)
            result = checked((result * 62) + CharToValue(c));

        return result;
    }

    /// <summary>Decodes the Base62 string to a byte array (Big Endian).</summary>
    /// <returns>A new byte array containing the decoded value.</returns>
    public byte[] ToBytes() {
        if(string.IsNullOrEmpty(this._value)) return [];

        BigInteger result = BigInteger.Zero;
        BigInteger multiplier = BigInteger.One;
        BigInteger base62 = new(62);

        for(int i = this._value.Length - 1; i >= 0; i--) {
            result += CharToValue(this._value[i]) * multiplier;
            multiplier *= base62;
        }

        return result.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CharToValue(char c) {
        if(c is >= '0' and <= '9') return c - '0';
        if(c is >= 'A' and <= 'Z') return c - 'A' + 10;
        if(c is >= 'a' and <= 'z') return c - 'a' + 36;
        throw new FormatException($"Invalid Base62 character: {c}");
    }

    #endregion

    #region Equality, Operators & ToString

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public bool Equals(Base62String other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return string.GetHashCode(this.Value.AsSpan(), StringComparison.Ordinal);
    }

    /// <summary>Implicitly converts a <see cref="Base62String"/> to a <see cref="string"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Base62String s) {
        return s.Value;
    }

    /// <summary>Implicitly converts a <see cref="Base62String"/> to a <see cref="ReadOnlySpan{Char}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(Base62String s) {
        return s.Value.AsSpan();
    }

    /// <summary>Explicitly converts a string to a <see cref="Base62String"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Base62String(string s) {
        return Parse(s);
    }

    /// <summary>Explicitly converts a long to a <see cref="Base62String"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Base62String(long l) {
        return FromInt64(l);
    }

    #endregion
}