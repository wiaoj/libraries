using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a structurally valid Base64Url string (RFC 4648, Section 5).
/// Immutable and guaranteed to contain only valid Base64Url characters (A-Z, a-z, 0-9, -, _).
/// </summary>
/// <remarks>
/// This value object combats "primitive obsession" by ensuring the contained value is always a valid Base64Url.
/// It leverages .NET 10's built-in <see cref="Base64Url"/> and <see cref="System.Text.Ascii"/> APIs
/// for highly optimized, zero-allocation, SIMD-accelerated encoding and decoding operations.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(Base64UrlStringJsonConverter))]
public readonly record struct Base64UrlString :
    IEquatable<Base64UrlString>,
    IComparable<Base64UrlString>,
    IComparable,
    ISpanParsable<Base64UrlString>,
    IUtf8SpanParsable<Base64UrlString>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<Base64UrlString, Base64UrlString, bool> {

    // Ultra-fast validation tables using SIMD (Vectorized operations)
    private static readonly SearchValues<char> ValidBase64UrlChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    private static readonly SearchValues<byte> ValidBase64UrlBytes =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"u8);

    private readonly string _encodedValue;

    /// <summary>
    /// Gets a value indicating whether this Base64Url string is empty or uninitialized (default).
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._encodedValue);

    /// <summary>
    /// Gets the number of characters in this Base64Url string.
    /// </summary>
    public int Length => this._encodedValue?.Length ?? 0;

    /// <summary>
    /// Gets the underlying Base64Url-encoded string value.
    /// Returns an empty string if the structure is default.
    /// </summary>
    public string Value => this._encodedValue ?? string.Empty;

    /// <summary>
    /// Gets an instance representing an empty Base64Url string.
    /// </summary>
    public static Base64UrlString Empty { get; } = new(string.Empty);

    // Private constructor ensures validation happens only through static factories.
    private Base64UrlString(string validatedValue) {
        this._encodedValue = validatedValue;
    }

    #region Creation

    /// <summary>
    /// Encodes a span of bytes into a <see cref="Base64UrlString"/> using high-performance buffer manipulation.
    /// </summary>
    /// <param name="bytes">The raw bytes to encode.</param>
    /// <returns>A new <see cref="Base64UrlString"/> instance containing the encoded data.</returns>
    [SkipLocalsInit]
    public static Base64UrlString FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) {
            return Empty;
        }

        int requiredLength = Base64Url.GetEncodedLength(bytes.Length);

        using ValueBuffer<byte> utf8Buffer = ValueBuffer.Create(requiredLength, stackalloc byte[1024]);

        if(Base64Url.EncodeToUtf8(bytes, utf8Buffer, out _, out int bytesWritten) == OperationStatus.Done) {
            return new Base64UrlString(Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]));
        }

        throw new InvalidOperationException("Failed to encode bytes to Base64Url.");
    }

    /// <summary>
    /// Encodes a plain UTF-8 string into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="text">The string text to encode.</param>
    /// <returns>A new <see cref="Base64UrlString"/> representing the encoded text.</returns>
    public static Base64UrlString FromUtf8(string text) {
        if(string.IsNullOrEmpty(text)) return Empty;
        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a plain UTF-8 byte span into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 bytes to encode.</param>
    /// <returns>A new <see cref="Base64UrlString"/> representing the encoded text.</returns>
    public static Base64UrlString FromUtf8(ReadOnlySpan<byte> utf8Text) {
        if(utf8Text.IsEmpty) return Empty;
        return FromBytes(utf8Text);
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns>A new <see cref="Base64UrlString"/> containing the encoded text.</returns>
    public static Base64UrlString From(string text, Encoding encoding) {
        if(string.IsNullOrEmpty(text)) {
            return Empty;
        }
        Preca.ThrowIfNull(encoding);
        return FromBytes(encoding.GetBytes(text));
    }

    #endregion

    #region Decoding (To Bytes)

    /// <summary>
    /// Decodes the Base64Url string into a newly allocated byte array.
    /// </summary>
    /// <returns>A new byte array containing the decoded binary data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBytes() {
        return Decode(this.Value.AsSpan());
    }

    /// <summary>
    /// Attempts to decode the Base64Url string into the provided destination span of bytes.
    /// </summary>
    /// <param name="destination">The buffer to receive the decoded bytes.</param>
    /// <param name="bytesWritten">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if decoding was successful; otherwise, <see langword="false"/>.</returns>
    [SkipLocalsInit]
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        return TryDecode(this.Value.AsSpan(), destination, out bytesWritten);
    }

    /// <summary>
    /// Gets the exact number of bytes that the decoded Base64Url string represents.
    /// </summary>
    /// <returns>The exact length of the decoded data in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetDecodedLength() {
        return GetDecodedLength(this.Length);
    }

    // ── Static, Span-Based (no Base64UrlString instance required) ─────────────

    /// <summary>
    /// Decodes a Base64Url-encoded character span directly into a newly allocated byte array,
    /// without constructing an intermediate <see cref="Base64UrlString"/> instance — avoids the
    /// extra string copy and validation pass that <see cref="Parse(ReadOnlySpan{char})"/> would
    /// otherwise perform.
    /// </summary>
    /// <param name="encoded">The Base64Url-encoded character span to decode.</param>
    /// <returns>A new byte array containing the decoded data.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="encoded"/> is not valid Base64Url.</exception>
    public static byte[] Decode(ReadOnlySpan<char> encoded) {
        if(encoded.IsEmpty) {
            return [];
        }

        byte[] result = new byte[GetDecodedLength(encoded.Length)];
        if(!TryDecode(encoded, result, out _)) {
            throw new FormatException("The input is not a valid Base64Url string.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to decode a Base64Url-encoded character span directly into a destination byte span,
    /// without constructing an intermediate <see cref="Base64UrlString"/> instance.
    /// </summary>
    /// <param name="encoded">The Base64Url-encoded character span to decode.</param>
    /// <param name="destination">The destination span to write decoded bytes into.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if decoding succeeded; otherwise, <see langword="false"/>.</returns>
    [SkipLocalsInit]
    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> destination, out int bytesWritten) {
        if(encoded.IsEmpty) {
            bytesWritten = 0;
            return true;
        }

        int charLen = encoded.Length;

        using ValueBuffer<byte> utf8Buffer = ValueBuffer.Create(charLen, stackalloc byte[1024]);

        // Highly optimized narrowing: Valid Base64Url is strictly ASCII.
        Ascii.FromUtf16(encoded, utf8Buffer, out _);

        return Base64Url.DecodeFromUtf8(utf8Buffer[..charLen], destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    private static int GetDecodedLength(int encodedLength) {
        if(encodedLength == 0) {
            return 0;
        }

        return (encodedLength * 3) / 4;
    }

    #endregion

    #region Parsing (Public Span API)

    /// <summary>
    /// Parses a string into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A new <see cref="Base64UrlString"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if the input is not a valid Base64Url format.</exception>
    public static Base64UrlString Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="Base64UrlString"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not a valid Base64Url format.</exception>
    public static Base64UrlString Parse(ReadOnlySpan<char> s) {
        if(TryParseInternal(s, out Base64UrlString result)) {
            return result;
        }
        throw new FormatException("The input is not a valid Base64Url string.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span to parse.</param>
    /// <returns>A valid <see cref="Base64UrlString"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not a valid Base64Url format.</exception>
    public static Base64UrlString Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParseInternal(utf8Text, out Base64UrlString result)) {
            return result;
        }
        throw new FormatException("The input is not a valid Base64Url UTF-8 sequence.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base64UrlString result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParseInternal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out Base64UrlString result) {
        return TryParseInternal(s, out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base64UrlString result) {
        return TryParseInternal(utf8Text, out result);
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Base64UrlString IParsable<Base64UrlString>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Base64UrlString>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base64UrlString result) => TryParse(s, out result);
    static Base64UrlString ISpanParsable<Base64UrlString>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Base64UrlString>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base64UrlString result) => TryParse(s, out result);
    static Base64UrlString IUtf8SpanParsable<Base64UrlString>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Base64UrlString>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base64UrlString result) => TryParse(utf8Text, out result);

    #endregion

    #region Internal Optimization Logic

    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out Base64UrlString result) {
        if(s.IsEmpty) {
            result = Empty;
            return true;
        }

        // SIMD hardware accelerated search instantly rejects invalid characters
        if(s.IndexOfAnyExcept(ValidBase64UrlChars) >= 0) {
            result = default;
            return false;
        }

        int maxDecodedLength = Base64Url.GetMaxDecodedLength(s.Length);

        using ValueBuffer<byte> utf8Buffer = ValueBuffer.Create(s.Length, stackalloc byte[1024]);
        using ValueBuffer<byte> decodeBuffer = ValueBuffer.Create(maxDecodedLength, stackalloc byte[1024]);

        Ascii.FromUtf16(s, utf8Buffer, out _);

        // Do a strict decode attempt to ensure no lingering logical errors (e.g., malformed padding states)
        if(Base64Url.DecodeFromUtf8(utf8Buffer[..s.Length], decodeBuffer, out _, out _) == OperationStatus.Done) {
            result = new Base64UrlString(s.ToString());
            return true;
        }

        result = default;
        return false;
    }

    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<byte> utf8Text, out Base64UrlString result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        if(utf8Text.IndexOfAnyExcept(ValidBase64UrlBytes) >= 0) {
            result = default;
            return false;
        }

        int maxDecodedLength = Base64Url.GetMaxDecodedLength(utf8Text.Length);

        using ValueBuffer<byte> decodeBuffer = ValueBuffer.Create(maxDecodedLength, stackalloc byte[1024]);

        if(Base64Url.DecodeFromUtf8(utf8Text, decodeBuffer, out _, out _) == OperationStatus.Done) {
            result = new Base64UrlString(Encoding.UTF8.GetString(utf8Text));
            return true;
        }

        result = default;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <summary>
    /// Formats the Base64Url string.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <returns>The underlying Base64Url string value.</returns>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Formats the Base64Url string using the specified format provider.
    /// </summary>
    /// <param name="format">The format string (ignored).</param>
    /// <param name="formatProvider">The format provider (ignored).</param>
    /// <returns>The underlying Base64Url string value.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

    /// <summary>
    /// Writes the strictly ASCII representation of the Base64Url string to the provided buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer to stream the data to.</param>
    public void WriteTo(IBufferWriter<byte> writer) {
        if(string.IsNullOrEmpty(this._encodedValue)) {
            return;
        }

        ReadOnlySpan<char> chars = this._encodedValue.AsSpan();
        int byteCount = chars.Length; // Guaranteed ASCII, so char count equals byte count

        Span<byte> buffer = writer.GetSpan(byteCount);

        // Zero-allocation, high performance char-to-byte downcasting
        System.Text.Ascii.FromUtf16(chars, buffer, out _);
        writer.Advance(byteCount);
    }

    /// <summary>
    /// Tries to format the Base64Url string into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the Base64Url string into the destination character span using the specified format.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the Base64Url string into the destination character span using the specified format and provider.
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
    /// Tries to format the Base64Url string into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the Base64Url string into the destination UTF-8 byte span using the specified format.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the Base64Url string into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="format">The format span (ignored).</param>
    /// <param name="provider">The format provider (ignored).</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._encodedValue)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._encodedValue.Length) { bytesWritten = 0; return false; }

        Ascii.FromUtf16(this._encodedValue.AsSpan(), utf8Destination, out bytesWritten);
        return true;
    }

    #endregion

    #region Comparison & Ordering

    /// <summary>
    /// Compares the current instance with another <see cref="Base64UrlString"/> using ordinal comparison.
    /// </summary>
    /// <param name="other">The other <see cref="Base64UrlString"/> to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(Base64UrlString other) => string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Compares the current instance with another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="Base64UrlString"/>.</exception>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Base64UrlString other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Base64UrlString)}", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Base64UrlString left, Base64UrlString right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Base64UrlString left, Base64UrlString right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Base64UrlString left, Base64UrlString right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Base64UrlString left, Base64UrlString right) => left.CompareTo(right) >= 0;

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Base64UrlString"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base64UrlString> OrdinalComparer => Base64UrlStringOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Base64UrlString"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Base64UrlString> OrdinalIgnoreCaseComparer => Base64UrlStringOrdinalIgnoreCaseComparer.Instance;

    private sealed class Base64UrlStringOrdinalComparer : IEqualityComparer<Base64UrlString>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base64UrlString> {
        public static Base64UrlStringOrdinalComparer Instance { get; } = new();

        public bool Equals(Base64UrlString x, Base64UrlString y) => x.Equals(y);

        public int GetHashCode(Base64UrlString obj) => obj.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, Base64UrlString other) => alternate.SequenceEqual(other.Value.AsSpan());

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.Ordinal);

        public Base64UrlString Create(ReadOnlySpan<char> alternate) => Base64UrlString.Parse(alternate);
    }

    private sealed class Base64UrlStringOrdinalIgnoreCaseComparer : IEqualityComparer<Base64UrlString>, IAlternateEqualityComparer<ReadOnlySpan<char>, Base64UrlString> {
        public static Base64UrlStringOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Base64UrlString x, Base64UrlString y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Base64UrlString obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public bool Equals(ReadOnlySpan<char> alternate, Base64UrlString other) => MemoryExtensions.Equals(alternate, other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public Base64UrlString Create(ReadOnlySpan<char> alternate) => Base64UrlString.Parse(alternate);
    }

    #endregion



    #region Equality and Operators

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <summary>
    /// Determines whether two <see cref="Base64UrlString"/> instances are equal.
    /// Treats <see langword="default"/> and <see cref="Empty"/> as equal.
    /// </summary>
    public bool Equals(Base64UrlString other) {
        if(this.IsEmpty && other.IsEmpty) return true;
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this.IsEmpty ? 0 : string.GetHashCode(this.Value.AsSpan(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base64UrlString"/> to its underlying <see cref="string"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Base64UrlString s) {
        return s.Value;
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base64UrlString"/> to a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(Base64UrlString s) {
        return s.Value.AsSpan();
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="Base64UrlString"/>.
    /// </summary>
    /// <exception cref="FormatException">Thrown if the provided string is not valid Base64Url.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Base64UrlString(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    #endregion
}