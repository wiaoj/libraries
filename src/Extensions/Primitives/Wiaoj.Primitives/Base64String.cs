using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    ISpanParsable<Base64String>,
    IUtf8SpanParsable<Base64String> {

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
        if (bytes.IsEmpty) {
            return Empty;
        }

        int requiredLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
        byte[]? rentedBytes = null;

        // Use stack memory for small buffers (<= 256 bytes), pool for larger ones.
        Span<byte> utf8Buffer = requiredLength <= 256
            ? stackalloc byte[requiredLength]
            : (rentedBytes = ArrayPool<byte>.Shared.Rent(requiredLength));

        try {
            if (Base64.EncodeToUtf8(bytes, utf8Buffer, out _, out int bytesWritten, isFinalBlock: true) == OperationStatus.Done) {
                return new Base64String(Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]));
            }

            throw new InvalidOperationException("Failed to encode bytes to Base64.");
        }
        finally {
            if (rentedBytes is not null) {
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
        if (string.IsNullOrEmpty(text)) {
            return Empty;
        }

        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns>The Base64 encoded representation of the text.</returns>
    public static Base64String From(string text, Encoding encoding) {
        if (string.IsNullOrEmpty(text)) {
            return Empty;
        }

        ArgumentNullException.ThrowIfNull(encoding);

        return FromBytes(encoding.GetBytes(text));
    }

    #endregion

    #region Parsing (Public Span API)

    /// <summary>
    /// Parses a character span into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="Base64String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not valid Base64.</exception>
    public static Base64String Parse(ReadOnlySpan<char> s) {
        if (TryParseInternal(s, out Base64String result)) {
            return result;
        }
        throw new FormatException("The input is not a valid Base64 string.");
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
    /// Parses a UTF-8 byte span into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span to parse.</param>
    /// <returns>A valid <see cref="Base64String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input is not valid Base64.</exception>
    public static Base64String Parse(ReadOnlySpan<byte> utf8Text) {
        if (TryParseInternal(utf8Text, out Base64String result)) {
            return result;
        }
        throw new FormatException("The input is not a valid Base64 UTF-8 sequence.");
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

    #region Internal Optimization Logic

    // Validates Base64 chars without allocating a new byte array for the result unless valid.
    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out Base64String result) {
        if (s.IsEmpty) {
            result = Empty;
            return true;
        }

        if (s.Length % 4 != 0) {
            result = default;
            return false;
        }

        int requiredByteCount = GetMaxDecodedLength(s.Length);

        // Use stack memory for validation of reasonable sizes (<= 1KB decoded)
        const int StackThreshold = 1024;

        byte[]? rented = null;
        Span<byte> decodeBuffer = requiredByteCount <= StackThreshold
            ? stackalloc byte[requiredByteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(requiredByteCount));

        try {
            // We just validate here. If successful, we create the string.
            if (Convert.TryFromBase64Chars(s, decodeBuffer, out _)) {
                result = new Base64String(s.ToString());
                return true;
            }
        }
        finally {
            if (rented is not null) {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        result = default;
        return false;
    }

    // Validates UTF-8 bytes without full allocation overhead.
    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<byte> utf8Text, out Base64String result) {
        if (utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        if (utf8Text.Length % 4 != 0) {
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
            if (Base64.DecodeFromUtf8(utf8Text, decodeBuffer, out _, out _, isFinalBlock: true) == OperationStatus.Done) {
                result = new Base64String(Encoding.UTF8.GetString(utf8Text));
                return true;
            }
        }
        finally {
            if (rented is not null) {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        result = default;
        return false;
    }

    #endregion

    #region Explicit Interface Implementations (Hidden API)

    // These explicit implementations ensure compatibility with generic parsing APIs 
    // without cluttering the public API surface with string-based overloads.

    static Base64String IParsable<Base64String>.Parse(string s, IFormatProvider? provider) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    static bool IParsable<Base64String>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base64String result) {
        if (s is null) {
            result = default;
            return false;
        }
        return TryParseInternal(s.AsSpan(), out result);
    }

    static Base64String ISpanParsable<Base64String>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<Base64String>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base64String result) {
        return TryParse(s, out result);
    }

    static Base64String IUtf8SpanParsable<Base64String>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<Base64String>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base64String result) {
        return TryParse(utf8Text, out result);
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
    /// Writes the UTF-8 representation of the Base64 string to the provided buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    public void WriteTo(IBufferWriter<byte> writer) {
        if (string.IsNullOrEmpty(this._encodedValue)) {
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
        if (encoded.IsEmpty) {
            return 0;
        }

        if (encoded[^1] == '=') {
            return (encoded.Length / 4 * 3) - (encoded[^2] == '=' ? 2 : 1);
        }
        return encoded.Length / 4 * 3;
    }

    private static int GetMaxDecodedLength(int encodedLength) {
        // (encodedLength * 3) / 4 roughly, but safer to over-estimate slightly for buffer sizing
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
        return this.Value.GetHashCode();
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
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    #endregion
}

/// <summary>
/// A custom JsonConverter for serializing and deserializing the <see cref="Base64String"/> struct efficiently.
/// </summary>
public sealed class Base64StringJsonConverter : JsonConverter<Base64String> {

    /// <inheritdoc/>
    public override Base64String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            // Optimization: Try to read directly from the ValueSpan (raw bytes) if possible
            // to avoid allocating an intermediate string before validation.
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            if (Base64String.TryParse(span, out Base64String result)) {
                return result;
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base64String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}