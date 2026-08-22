using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a string containing only valid Base32 characters (RFC 4648).
/// </summary>
/// <remarks>
/// This value object ensures that any instance of <see cref="Base32String"/> holds a structurally valid 
/// Base32 string. Since .NET does not have a built-in Base32 codec, this struct implements a 
/// high-performance, allocation-optimized RFC 4648 encoder/decoder internally, utilizing .NET 8 <see cref="SearchValues{T}"/>.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(Base32StringJsonConverter))]
public readonly record struct Base32String :
    IEquatable<Base32String>,
    IComparable<Base32String>,
    IComparable,
    ISpanParsable<Base32String>,
    IUtf8SpanParsable<Base32String>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<Base32String, Base32String, bool> {

    // 1. Valid characters for input (Case-insensitive + Padding)
    private static readonly SearchValues<char> InputBase32Chars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz234567=");

    // 2. Valid bytes for UTF-8 input
    private static readonly SearchValues<byte> InputBase32Bytes =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz234567="u8);

    // 3. To check if we need ToUpper conversion
    private static readonly SearchValues<char> LowerCaseLetters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyz");

    private readonly string _encodedValue;

    /// <summary>
    /// Represents an empty Base32 string.
    /// </summary>
    public static Base32String Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the underlying Base32-encoded string value.
    /// </summary>
    public string Value => this._encodedValue ?? string.Empty;

    // The constructor is private to ensure all creation goes through validation factory methods.
    private Base32String(string validatedValue) {
        this._encodedValue = validatedValue;
    }

    // RFC 4648 Alphabet
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    // Lookup table for fast decoding (0xFF indicates invalid/ignored in decode phase)
    private static ReadOnlySpan<byte> DecodeTable => [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0-7
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 8-15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 16-23
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 24-31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 32-39
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 40-47
        0xFF, 0xFF, 26,   27,   28,   29,   30,   31,   // 48-55 ('0'-'7') -> '2'-'7'
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 56-63
        0xFF, 0,    1,    2,    3,    4,    5,    6,    // 64-71 ('@', 'A'-'G')
        7,    8,    9,    10,   11,   12,   13,   14,   // 72-79 ('H'-'O')
        15,   16,   17,   18,   19,   20,   21,   22,   // 80-87 ('P'-'W')
        23,   24,   25,   0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 88-95 ('X'-'Z')
        0xFF, 0,    1,    2,    3,    4,    5,    6,    // 96-103 ('a'-'g')
        7,    8,    9,    10,   11,   12,   13,   14,   // 104-111 ('h'-'o')
        15,   16,   17,   18,   19,   20,   21,   22,   // 112-119 ('p'-'w')
        23,   24,   25,   0xFF, 0xFF, 0xFF, 0xFF, 0xFF  // 120-127 ('x'-'z')
    ];

    #region Creation  

    /// <summary>
    /// Encodes a span of bytes into a Base32 string.
    /// </summary>
    /// <param name="bytes">The data to encode.</param>
    /// <returns>A new <see cref="Base32String"/> instance.</returns>
    [SkipLocalsInit]
    public static Base32String FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty)
            return Empty;

        int charCount = (bytes.Length * 8 + 4) / 5;
        int padding = (bytes.Length % 5) switch { 0 => 0, 1 => 6, 2 => 4, 3 => 3, 4 => 1, _ => 0 };

        // string.Create allows us to write directly to the string's memory buffer
        return new Base32String(string.Create(charCount + padding, bytes, (chars, input) => {
            ReadOnlySpan<byte> data = input;
            int bitIndex = 0;
            int inputBitLength = data.Length * 8;
            int outputIndex = 0;

            while(bitIndex < inputBitLength) {
                int byteIndex = bitIndex / 8;
                int bitOffset = bitIndex % 8;
                int b = data[byteIndex];
                int val;

                if(bitOffset <= 3)
                    val = (b >> (3 - bitOffset)) & 0x1F;
                else {
                    val = (b << (bitOffset - 3)) & 0x1F;
                    if(byteIndex + 1 < data.Length)
                        val |= data[byteIndex + 1] >> (11 - bitOffset);
                }

                chars[outputIndex++] = Alphabet[val];
                bitIndex += 5;
            }

            // Apply padding
            while(outputIndex < chars.Length)
                chars[outputIndex++] = '=';
        }));
    }

    /// <summary>
    /// Encodes a plain UTF-8 string into a Base32String.
    /// Example: FromUtf8("hello") -> "NBSWY3DP"
    /// </summary>
    public static Base32String FromUtf8(string text) {
        if(string.IsNullOrEmpty(text))
            return Empty;
        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a Base32 string.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns>A new <see cref="Base32String"/> instance containing the encoded data.</returns>
    public static Base32String From(string text, Encoding encoding) {
        if(string.IsNullOrEmpty(text))
            return Empty;
        Preca.ThrowIfNull(encoding);
        return FromBytes(encoding.GetBytes(text));
    }
    #endregion

    #region Parsing (From Text)

    /// <summary>
    /// Parses a string into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A new <see cref="Base32String"/> instance.</returns>
    /// <exception cref="FormatException">Thrown if the string contains invalid Base32 characters.</exception>
    public static Base32String Parse(string s) {
        Preca.ThrowIfNull(s);
        if(TryParse(s.AsSpan(), out Base32String result))
            return result;
        throw new FormatException("The input string is not a valid Base32 string.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="Base32String"/> if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base32String result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Parses a character span into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A new <see cref="Base32String"/> instance.</returns>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base32 characters.</exception>
    public static Base32String Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out Base32String result))
            return result;
        throw new FormatException("The input is not a valid Base32 string.");
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="Base32String"/> if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out Base32String result) {
        if(s.IsEmpty) {
            result = Empty;
            return true;
        }

        if(s.IndexOfAnyExcept(InputBase32Chars) >= 0) {
            result = default;
            return false;
        }

        int paddingIndex = s.IndexOf('=');
        if(paddingIndex >= 0) {
            ReadOnlySpan<char> tail = s[paddingIndex..];
            foreach(char c in tail) {
                if(c != '=') {
                    result = default;
                    return false;
                }
            }
        }

        if(s.IndexOfAny(LowerCaseLetters) < 0) {
            result = new Base32String(s.ToString());
            return true;
        }

        result = new Base32String(s.ToString().ToUpperInvariant());
        return true;
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A new <see cref="Base32String"/> instance.</returns>
    /// <exception cref="FormatException">Thrown if the input is not a valid Base32 sequence.</exception>
    public static Base32String Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out Base32String result))
            return result;
        throw new FormatException("The input is not a valid Base32 string.");
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="Base32String"/> if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base32String result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        if(utf8Text.IndexOfAnyExcept(InputBase32Bytes) >= 0) {
            result = default;
            return false;
        }

        return TryParse(Encoding.UTF8.GetString(utf8Text).AsSpan(), out result);
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Base32String IParsable<Base32String>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Base32String>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base32String result) => TryParse(s, out result);
    static Base32String ISpanParsable<Base32String>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Base32String>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base32String result) => TryParse(s, out result);
    static Base32String IUtf8SpanParsable<Base32String>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Base32String>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base32String result) => TryParse(utf8Text, out result);

    #endregion

    #region Decoding (To Bytes)

    /// <summary>
    /// Decodes the Base32 string into a newly allocated byte array.
    /// </summary>
    /// <returns>A byte array containing the decoded binary data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBytes() {
        if(this.Value.Length == 0)
            return [];

        byte[] result = new byte[GetDecodedLength()];
        TryDecode(result, out _);
        return result;
    }

    /// <summary>
    /// Attempts to decode the Base32 string into the provided destination span.
    /// </summary>
    /// <param name="destination">The destination span to write decoded bytes to.</param>
    /// <param name="bytesWritten">The number of bytes successfully written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if decoding was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        bytesWritten = 0;
        if(this.Value.Length == 0)
            return true;

        int requiredLen = GetDecodedLength();
        if(destination.Length < requiredLen)
            return false;

        ReadOnlySpan<char> src = this.Value.AsSpan();

        int buffer = 0;
        int bitsLeft = 0;
        int outIndex = 0;

        for(int i = 0; i < src.Length; i++) {
            char c = src[i];
            if(c == '=')
                break;

            int val = c < DecodeTable.Length ? DecodeTable[c] : 0xFF;
            if(val == 0xFF)
                return false;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if(bitsLeft >= 8) {
                destination[outIndex++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                bitsLeft -= 8;
            }
        }

        bytesWritten = outIndex;
        return true;
    }

    /// <summary>
    /// Calculates the exact number of bytes that this Base32 string will decode to.
    /// </summary>
    /// <returns>The number of bytes resulting from decoding.</returns>
    public int GetDecodedLength() {
        if(this.Value.Length == 0)
            return 0;
        int padding = 0;
        for(int i = this.Value.Length - 1; i >= 0; i--) {
            if(this.Value[i] == '=')
                padding++;
            else
                break;
        }
        return (this.Value.Length - padding) * 5 / 8;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <summary>
    /// Formats the Base32 string.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <returns>The Base32 encoded string value.</returns>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the Base32 string using the specified format provider.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns>The Base32 encoded string value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

    /// <summary>
    /// Tries to format the Base32 string into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the Base32 string into the destination character span using the specified format.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the Base32 string into the destination character span using the specified format and provider.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <param name="provider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        ReadOnlySpan<char> src = this.Value.AsSpan();
        if(destination.Length < src.Length) { charsWritten = 0; return false; }
        src.CopyTo(destination);
        charsWritten = src.Length;
        return true;
    }

    /// <summary>
    /// Tries to format the Base32 string into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the Base32 string into the destination UTF-8 byte span using the specified format.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the Base32 string into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <param name="provider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._encodedValue)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._encodedValue.Length) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(this._encodedValue.AsSpan(), utf8Destination);
        return true;
    }

    #endregion

    #region Comparison & Ordering

    /// <summary>
    /// Compares the current instance with another <see cref="Base32String"/> using ordinal comparison.
    /// </summary>
    /// <param name="other">The other <see cref="Base32String"/> to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(Base32String other) => string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares the current instance with another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="Base32String"/>.</exception>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Base32String other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Base32String)}", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Base32String left, Base32String right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Base32String left, Base32String right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Base32String left, Base32String right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Base32String left, Base32String right) => left.CompareTo(right) >= 0;

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Base32String"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base32String> OrdinalComparer => Base32StringOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Base32String"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base32String> OrdinalIgnoreCaseComparer => Base32StringOrdinalIgnoreCaseComparer.Instance;

    private sealed class Base32StringOrdinalComparer : IEqualityComparer<Base32String>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base32String> {
        public static Base32StringOrdinalComparer Instance { get; } = new();

        public bool Equals(Base32String x, Base32String y) => string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(Base32String obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.Ordinal);

        public bool Equals(ReadOnlySpan<char> alternate, Base32String other) => alternate.SequenceEqual(other.Value.AsSpan());

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.Ordinal);

        public Base32String Create(ReadOnlySpan<char> alternate) => Base32String.Parse(alternate);
    }

    private sealed class Base32StringOrdinalIgnoreCaseComparer : IEqualityComparer<Base32String>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base32String> {
        public static Base32StringOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Base32String x, Base32String y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Base32String obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public bool Equals(ReadOnlySpan<char> alternate, Base32String other) => MemoryExtensions.Equals(alternate, other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public Base32String Create(ReadOnlySpan<char> alternate) => Base32String.Parse(alternate);
    }

    #endregion

    #region Formatting, Equality and Operators

    /// <summary>
    /// Writes the UTF-8 representation of the Base32 string to the provided buffer writer.
    /// </summary>
    public void WriteTo(IBufferWriter<byte> writer) {
        if(string.IsNullOrEmpty(_encodedValue))
            return;

        ReadOnlySpan<char> chars = _encodedValue.AsSpan();
        int byteCount = Encoding.UTF8.GetByteCount(chars);

        Span<byte> buffer = writer.GetSpan(byteCount);
        int written = Encoding.UTF8.GetBytes(chars, buffer);
        writer.Advance(written);
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public bool Equals(Base32String other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return string.GetHashCode(this.Value.AsSpan(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base32String"/> to its underlying <see cref="string"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Base32String s) {
        return s.Value;
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base32String"/> to a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(Base32String s) {
        return s.Value.AsSpan();
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="Base32String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Base32String(string s) {
        return Parse(s);
    }

    #endregion
}