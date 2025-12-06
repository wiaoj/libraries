using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <remarks>
/// This value object fights "primitive obsession" by ensuring that any instance of <see cref="Base64String"/>
/// holds a structurally valid Base64 string, thus eliminating the need for repeated validation
/// throughout an application. 
/// 
/// It provides highly efficient, allocation-free parsing methods
/// for <see cref="ReadOnlySpan{Char}"/> and UTF-8 <see cref="ReadOnlySpan{Byte}"/>, 
/// making it ideal for performance-critical scenarios like web applications.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(Base64StringJsonConverter))]
public readonly record struct Base64String : IEquatable<Base64String> {
    private readonly string _encodedValue;

    /// <summary>
    /// Represents an empty Base64 string.
    /// </summary>
    public static Base64String Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the underlying Base64-encoded string value.
    /// Returns an empty string if the structure was created with its default constructor.
    /// </summary>
    public string Value => this._encodedValue ?? string.Empty;

    // The constructor is private to ensure all creation goes through validation factory methods.
    private Base64String(string validatedValue) {
        this._encodedValue = validatedValue;
    }

    #region Creation 

    /// <summary>
    /// Encodes a span of bytes into a Base64 string using the high-performance System.Buffers.Text.Base64 API.
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
        try {
            Span<byte> utf8Buffer = requiredLength <= 256
                ? stackalloc byte[requiredLength]
                : (rentedBytes = ArrayPool<byte>.Shared.Rent(requiredLength));

            if (Base64.EncodeToUtf8(bytes, utf8Buffer, out _, out int bytesWritten, isFinalBlock: true) == OperationStatus.Done) {
                return new Base64String(Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]));
            }
            // This path should not be hit with a large enough buffer.
            throw new InvalidOperationException("Failed to encode bytes to Base64.");
        }
        finally {
            if (rentedBytes is not null) {
                ArrayPool<byte>.Shared.Return(rentedBytes);
            }
        }
    }

    /// <summary>
    /// Encodes a plain UTF-8 string into a Base64String.
    /// Example: FromUtf8("Hello") -> "SGVsbG8="
    /// </summary>
    public static Base64String FromUtf8(string text) {
        if (string.IsNullOrEmpty(text)) {
            return Empty;
        }

        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a Base64String.
    /// Example: From("Hello", Encoding.ASCII) -> "SGVsbG8="
    /// </summary>
    public static Base64String From(string text, Encoding encoding) {
        if (string.IsNullOrEmpty(text)) {
            return Empty;
        }

        Preca.ThrowIfNull(encoding);

        return FromBytes(encoding.GetBytes(text));
    }
    #endregion

    #region Parsing (From Text)
    /// <summary>
    /// Writes the UTF-8 representation of the Base64 string to the provided buffer writer.
    /// </summary>
    public void WriteTo(IBufferWriter<byte> writer) {
        if (string.IsNullOrEmpty(_encodedValue))
            return;

        ReadOnlySpan<char> chars = _encodedValue.AsSpan();
        int byteCount = chars.Length; // Base64 is ASCII

        Span<byte> buffer = writer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(chars, buffer);
        writer.Advance(byteCount);
    }

    /// <summary>
    /// Parses a string into a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A <see cref="Base64String"/> instance representing the parsed value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The input string is not in a valid Base64 format.</exception>
    public static Base64String Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if (TryParse(s.AsSpan(), out Base64String result)) {
            return result;
        }

        throw new FormatException("The input string is not a valid Base64 string.");
    }

    /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base64String result) {
        if (s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)"/>
    public static Base64String Parse(ReadOnlySpan<char> s) {
        if (TryParse(s, out Base64String result)) {
            return result;
        }

        throw new FormatException("The input is not a valid Base64 string.");
    }

    /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)"/>
    [SkipLocalsInit]
    public static bool TryParse(ReadOnlySpan<char> s, out Base64String result) {
        // For char-based validation, Convert is the most direct API.
        if (s.Length % 4 != 0) { result = default; return false; }

        Span<byte> tempBuffer = stackalloc byte[GetMaxDecodedLength(s.Length)];
        if (Convert.TryFromBase64Chars(s, tempBuffer, out _)) {
            result = new Base64String(s.ToString());
            return true;
        }
        result = default;
        return false;
    }

    /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.Parse(ReadOnlySpan{byte}, IFormatProvider?)"/>
    public static Base64String Parse(ReadOnlySpan<byte> utf8Text) {
        if (TryParse(utf8Text, out Base64String result)) {
            return result;
        }

        throw new FormatException("The input is not a valid Base64 string.");
    }

    /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.TryParse(ReadOnlySpan{byte}, IFormatProvider?, out TSelf)"/>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base64String result) {
        // The modern API lets us validate without decoding by using an empty destination.
        // If the source is valid, it returns NeedMoreData (because dest is empty).
        if (Base64.DecodeFromUtf8(utf8Text, [], out _, out _, isFinalBlock: true) == OperationStatus.NeedMoreData) {
            result = new Base64String(Encoding.UTF8.GetString(utf8Text));
            return true;
        }

        // Handle the edge case of an empty input, which is valid.
        if (utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        result = default;
        return false;
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
        return ((encodedLength * 3) + 3) / 4;
    }

    #endregion

    #region Formatting, Equality and Operators

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public bool Equals(Base64String other) {
        return this.Value == other.Value;
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
    /// <exception cref="FormatException">The provided string is not in a valid Base64 format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Base64String(string s) {
        return Parse(s);
    }

    #endregion
}

/// <summary>
/// A custom JsonConverter for serializing and deserializing the <see cref="Base64String"/> struct.
/// </summary>
public sealed class Base64StringJsonConverter : JsonConverter<Base64String> {
    public override Base64String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? value = reader.GetString();
        return value is null ? default : Base64String.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, Base64String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}