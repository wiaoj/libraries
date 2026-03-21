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
    ISpanParsable<Base62String>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IUtf8SpanParsable<Base62String>,
    IFormattable {

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

    #region Parsing (Public — no IFormatProvider)

    /// <summary>Parses a string into a <see cref="Base62String"/>.</summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 characters.</exception>
    public static Base62String Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if(TryParse(s.AsSpan(), out Base62String result)) return result;
        throw new FormatException($"Invalid Base62 string: '{s}'");
    }

    /// <summary>Parses a character span into a <see cref="Base62String"/>.</summary>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 characters.</exception>
    public static Base62String Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out Base62String result)) return result;
        throw new FormatException("Invalid Base62 string.");
    }

    /// <summary>Tries to parse a string into a <see cref="Base62String"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base62String result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a character span into a <see cref="Base62String"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Base62String result) {
        if(s.IsEmpty) { result = Empty; return true; }
        if(s.IndexOfAnyExcept(Base62Chars) >= 0) { result = default; return false; }
        result = new Base62String(s.ToString());
        return true;
    }

    /// <summary>Tries to parse a UTF-8 byte span into a <see cref="Base62String"/>.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base62String result) {
        if(utf8Text.IsEmpty) { result = Empty; return true; }
        if(utf8Text.IndexOfAnyExcept(Base62Utf8Bytes) >= 0) { result = default; return false; }
        result = new Base62String(Encoding.UTF8.GetString(utf8Text));
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IFormatProvider hidden from public API)

    // IParsable
    static Base62String IParsable<Base62String>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<Base62String>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base62String result) {
        return TryParse(s, out result);
    }

    // ISpanParsable
    static Base62String ISpanParsable<Base62String>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<Base62String>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base62String result) {
        return TryParse(s, out result);
    }

    // IUtf8SpanParsable
    static Base62String IUtf8SpanParsable<Base62String>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if(TryParse(utf8Text, out Base62String result)) return result;
        throw new FormatException("Invalid Base62 UTF-8 sequence.");
    }

    static bool IUtf8SpanParsable<Base62String>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base62String result) {
        return TryParse(utf8Text, out result);
    }

    // IFormattable — Base62 is culture-invariant, format and provider ignored
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    // ISpanFormattable — provider ignored
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    // IUtf8SpanFormattable — Base62 is ASCII subset so byte count == char count
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Destination);
        return true;
    }

    #endregion

    #region Decoding

    /// <summary>
    /// Decodes the Base62 string back to a <see cref="long"/>.
    /// Uses Horner's Method for high performance and accurate overflow detection.
    /// </summary>
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
        return this.Value.GetHashCode(StringComparison.Ordinal);
    }

    /// <summary>Implicitly converts a <see cref="Base62String"/> to a <see cref="string"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Base62String s) {
        return s.Value;
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