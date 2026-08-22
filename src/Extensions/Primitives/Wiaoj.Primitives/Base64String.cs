using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a structurally valid Base64 string.
/// This value object eliminates "primitive obsession" by ensuring the contained value is always valid Base64.
/// </summary>
/// <remarks>
/// It implements <see cref="ISpanParsable{TSelf}"/> and <see cref="IUtf8SpanParsable{TSelf}"/> 
/// to provide high-performance, low-allocation validation logic, making it ideal for high-throughput web APIs.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(Base64StringJsonConverter))]
public readonly record struct Base64String :
    IEquatable<Base64String>,
    IComparable<Base64String>,
    IComparable,
    ISpanParsable<Base64String>,
    IUtf8SpanParsable<Base64String>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<Base64String, Base64String, bool> {

    private readonly string _encodedValue;

    /// <summary>
    /// Gets an instance representing an empty Base64 string.
    /// </summary>
    public static Base64String Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the underlying Base64-encoded string value.
    /// Returns an empty string if the structure is default.
    /// </summary>
    public string Value => this._encodedValue ?? string.Empty;

    // Private constructor ensures validation happens only through static factories.
    private Base64String(string validatedValue) {
        this._encodedValue = validatedValue;
    }

    #region Creation

    /// <summary>
    /// Encodes a span of bytes into a <see cref="Base64String"/> using high-performance buffer manipulation.
    /// </summary>
    /// <param name="bytes">The raw bytes to encode.</param>
    /// <returns>A new <see cref="Base64String"/> instance containing the encoded data.</returns>
    [SkipLocalsInit]
    public static Base64String FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) {
            return Empty;
        }

        int requiredLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
        byte[]? rentedBytes = null;

        // Use stack memory for small buffers (<= 256 bytes), pool for larger ones.
        Span<byte> utf8Buffer = requiredLength <= 256
            ? stackalloc byte[requiredLength]
            : (rentedBytes = ArrayPool<byte>.Shared.Rent(requiredLength));

        try {
            if(Base64.EncodeToUtf8(bytes, utf8Buffer, out _, out int bytesWritten, isFinalBlock: true) == OperationStatus.Done) {
                return new Base64String(Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]));
            }

            throw new InvalidOperationException("Failed to encode bytes to Base64.");
        }
        finally {
            if(rentedBytes is not null) {
                ArrayPool<byte>.Shared.Return(rentedBytes);
            }
        }
    }

    /// <summary>
    /// Encodes a UTF-8 string into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>The Base64 encoded representation of the text.</returns>
    public static Base64String FromUtf8(string text) {
        if(string.IsNullOrEmpty(text)) {
            return Empty;
        }

        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <summary>
    /// Encodes a string using the specified encoding into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns>The Base64 encoded representation of the text.</returns>
    public static Base64String From(string text, Encoding encoding) {
        if(string.IsNullOrEmpty(text)) {
            return Empty;
        }

        Preca.ThrowIfNull(encoding);

        return FromBytes(encoding.GetBytes(text));
    }

    #endregion

    #region Parsing (Public Span API)

    /// <summary>
    /// Parses a string into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A new <see cref="Base64String"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if s is null.</exception>
    /// <exception cref="FormatException">Thrown if the input is not valid Base64.</exception>
    public static Base64String Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="Base64String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not valid Base64.</exception>
    public static Base64String Parse(ReadOnlySpan<char> s) {
        if(TryParseInternal(s, out Base64String result)) {
            return result;
        }
        throw new FormatException("The input is not a valid Base64 string.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span to parse.</param>
    /// <returns>A valid <see cref="Base64String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not valid Base64.</exception>
    public static Base64String Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParseInternal(utf8Text, out Base64String result)) {
            return result;
        }
        throw new FormatException("The input is not a valid Base64 UTF-8 sequence.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result if successful.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base64String result) {
        if(s is null) {
            result = default;
            return false;
        }

        return TryParseInternal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out Base64String result) {
        return TryParseInternal(s, out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base64String result) {
        return TryParseInternal(utf8Text, out result);
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Base64String IParsable<Base64String>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Base64String>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base64String result) => TryParse(s, out result);
    static Base64String ISpanParsable<Base64String>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Base64String>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base64String result) => TryParse(s, out result);
    static Base64String IUtf8SpanParsable<Base64String>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Base64String>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base64String result) => TryParse(utf8Text, out result);

    #endregion

    #region Internal Optimization Logic

    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out Base64String result) {
        if(s.IsEmpty) {
            result = Empty;
            return true;
        }

        if(s.Length % 4 != 0) {
            result = default;
            return false;
        }

        int requiredByteCount = GetMaxDecodedLength(s.Length);
        const int StackThreshold = 1024;

        byte[]? rented = null;
        Span<byte> decodeBuffer = requiredByteCount <= StackThreshold
            ? stackalloc byte[requiredByteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(requiredByteCount));

        try {
            if(Convert.TryFromBase64Chars(s, decodeBuffer, out _)) {
                result = new Base64String(s.ToString());
                return true;
            }
        }
        finally {
            if(rented is not null) {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        result = default;
        return false;
    }

    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<byte> utf8Text, out Base64String result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        if(utf8Text.Length % 4 != 0) {
            result = default;
            return false;
        }

        int requiredByteCount = GetMaxDecodedLength(utf8Text.Length);
        const int StackThreshold = 1024;

        byte[]? rented = null;
        Span<byte> decodeBuffer = requiredByteCount <= StackThreshold
            ? stackalloc byte[requiredByteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(requiredByteCount));

        try {
            if(Base64.DecodeFromUtf8(utf8Text, decodeBuffer, out _, out _, isFinalBlock: true) == OperationStatus.Done) {
                result = new Base64String(Encoding.UTF8.GetString(utf8Text));
                return true;
            }
        }
        finally {
            if(rented is not null) {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        result = default;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <summary>
    /// Formats the Base64 string.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <returns>The underlying Base64 string value.</returns>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the Base64 string using the specified format provider.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns>The underlying Base64 string value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

    /// <summary>
    /// Tries to format the Base64 string into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the Base64 string into the destination character span using the specified format.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the Base64 string into the destination character span using the specified format and provider.
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
    /// Tries to format the Base64 string into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the Base64 string into the destination UTF-8 byte span using the specified format.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the Base64 string into the destination UTF-8 byte span using the specified format and provider.
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
    /// Compares the current instance with another <see cref="Base64String"/> using ordinal comparison.
    /// </summary>
    /// <param name="other">The other <see cref="Base64String"/> to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(Base64String other) => string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares the current instance with another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="Base64String"/>.</exception>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Base64String other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Base64String)}", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Base64String left, Base64String right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Base64String left, Base64String right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Base64String left, Base64String right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Base64String left, Base64String right) => left.CompareTo(right) >= 0;

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Base64String"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base64String> OrdinalComparer => Base64StringOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Base64String"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base64String> OrdinalIgnoreCaseComparer => Base64StringOrdinalIgnoreCaseComparer.Instance;

    private sealed class Base64StringOrdinalComparer : IEqualityComparer<Base64String>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base64String> {
        public static Base64StringOrdinalComparer Instance { get; } = new();

        public bool Equals(Base64String x, Base64String y) => string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(Base64String obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.Ordinal);

        public bool Equals(ReadOnlySpan<char> alternate, Base64String other) => alternate.SequenceEqual(other.Value.AsSpan());

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.Ordinal);

        public Base64String Create(ReadOnlySpan<char> alternate) => Base64String.Parse(alternate);
    }

    private sealed class Base64StringOrdinalIgnoreCaseComparer : IEqualityComparer<Base64String>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base64String> {
        public static Base64StringOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Base64String x, Base64String y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Base64String obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public bool Equals(ReadOnlySpan<char> alternate, Base64String other) => MemoryExtensions.Equals(alternate, other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public Base64String Create(ReadOnlySpan<char> alternate) => Base64String.Parse(alternate);
    }

    #endregion

    #region Decoding (To Bytes)

    /// <summary>
    /// Decodes the Base64 string into a newly allocated byte array.
    /// </summary>
    /// <returns>A new byte array containing the decoded data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBytes() {
        return Convert.FromBase64String(this.Value);
    }

    /// <summary>
    /// Decodes a Base64-encoded character span directly into a newly allocated byte array,
    /// without constructing an intermediate <see cref="Base64String"/> instance  avoids the
    /// extra string copy that <see cref="Parse(ReadOnlySpan{char})"/> would otherwise allocate.
    /// </summary>
    /// <param name="encoded">The Base64-encoded character span to decode.</param>
    /// <returns>A new byte array containing the decoded data.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="encoded"/> is not valid Base64.</exception>
    public static byte[] Decode(ReadOnlySpan<char> encoded) {
        if(encoded.IsEmpty) {
            return [];
        }

        byte[] result = new byte[GetDecodedLength(encoded)];
        if(!Convert.TryFromBase64Chars(encoded, result, out _)) {
            throw new FormatException("The input is not a valid Base64 string.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to decode a Base64-encoded character span directly into a destination byte span,
    /// with ZERO heap allocations  no intermediate <see cref="Base64String"/> or byte array is created.
    /// </summary>
    /// <param name="encoded">The Base64-encoded character span to decode.</param>
    /// <param name="destination">The destination span to write decoded bytes into.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if decoding succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> destination, out int bytesWritten) {
        if(encoded.IsEmpty) {
            bytesWritten = 0;
            return true;
        }

        return Convert.TryFromBase64Chars(encoded, destination, out bytesWritten);
    }

    /// <summary>
    /// Writes the UTF-8 representation of the Base64 string to the provided buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    public void WriteTo(IBufferWriter<byte> writer) {
        if(string.IsNullOrEmpty(this._encodedValue)) {
            return;
        }

        ReadOnlySpan<char> chars = this._encodedValue.AsSpan();
        int byteCount = chars.Length;

        Span<byte> buffer = writer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(chars, buffer);
        writer.Advance(byteCount);
    }

    /// <summary>
    /// Attempts to decode the Base64 string into the provided destination span of bytes.
    /// </summary>
    /// <param name="destination">The buffer to receive the decoded bytes.</param>
    /// <param name="bytesWritten">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if decoding was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        return Convert.TryFromBase64String(this.Value, destination, out bytesWritten);
    }

    /// <summary>
    /// Gets the exact number of bytes that the decoded Base64 string represents.
    /// </summary>
    /// <returns>The length of the decoded data in bytes.</returns>
    public int GetDecodedLength() {
        return GetDecodedLength(this.Value.AsSpan());
    }

    private static int GetDecodedLength(ReadOnlySpan<char> encoded) {
        if(encoded.IsEmpty) {
            return 0;
        }

        if(encoded[^1] == '=') {
            return (encoded.Length / 4 * 3) - (encoded[^2] == '=' ? 2 : 1);
        }
        return encoded.Length / 4 * 3;
    }

    private static int GetMaxDecodedLength(int encodedLength) {
        return (encodedLength >> 2) * 3;
    }

    #endregion

    #region Formatting, Equality and Operators

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public bool Equals(Base64String other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return string.GetHashCode(this.Value.AsSpan(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base64String"/> to its underlying <see cref="string"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Base64String s) {
        return s.Value;
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base64String"/> to a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(Base64String s) {
        return s.Value.AsSpan();
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <exception cref="FormatException">The provided string is not in a valid Base64 format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Base64String(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    #endregion
}