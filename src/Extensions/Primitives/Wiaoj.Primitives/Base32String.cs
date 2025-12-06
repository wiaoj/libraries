using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
public readonly record struct Base32String : IEquatable<Base32String> {

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
        if (bytes.IsEmpty)
            return Empty;

        int charCount = (bytes.Length * 8 + 4) / 5;
        int padding = (bytes.Length % 5) switch { 0 => 0, 1 => 6, 2 => 4, 3 => 3, 4 => 1, _ => 0 };

        // string.Create allows us to write directly to the string's memory buffer
        return new Base32String(string.Create(charCount + padding, bytes, (chars, input) => {
            ReadOnlySpan<byte> data = input;
            int bitIndex = 0;
            int inputBitLength = data.Length * 8;
            int outputIndex = 0;

            while (bitIndex < inputBitLength) {
                int byteIndex = bitIndex / 8;
                int bitOffset = bitIndex % 8;
                int b = data[byteIndex];
                int val;

                if (bitOffset <= 3)
                    val = (b >> (3 - bitOffset)) & 0x1F;
                else {
                    val = (b << (bitOffset - 3)) & 0x1F;
                    if (byteIndex + 1 < data.Length)
                        val |= data[byteIndex + 1] >> (11 - bitOffset);
                }

                chars[outputIndex++] = Alphabet[val];
                bitIndex += 5;
            }

            // Apply padding
            while (outputIndex < chars.Length)
                chars[outputIndex++] = '=';
        }));
    }

    /// <summary>
    /// Encodes a plain UTF-8 string into a Base32String.
    /// Example: FromUtf8("hello") -> "NBSWY3DP"
    /// </summary>
    public static Base32String FromUtf8(string text) {
        if (string.IsNullOrEmpty(text))
            return Empty;
        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a Base32String.
    /// </summary>
    public static Base32String From(string text, Encoding encoding) {
        if (string.IsNullOrEmpty(text))
            return Empty;
        ArgumentNullException.ThrowIfNull(encoding);
        return FromBytes(encoding.GetBytes(text));
    }
    #endregion

    #region Parsing (From Text)

    /// <summary>
    /// Parses a string into a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <exception cref="FormatException">Thrown if the string contains invalid Base32 characters.</exception>
    public static Base32String Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if (TryParse(s.AsSpan(), out Base32String result))
            return result;
        throw new FormatException("The input string is not a valid Base32 string.");
    }

    /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, out Base32String result) {
        if (s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)"/>
    public static Base32String Parse(ReadOnlySpan<char> s) {
        if (TryParse(s, out Base32String result))
            return result;
        throw new FormatException("The input is not a valid Base32 string.");
    }

    /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)"/>
    public static bool TryParse(ReadOnlySpan<char> s, out Base32String result) {
        if (s.IsEmpty) {
            result = Empty;
            return true;
        }

        // Optimization 1: Use Vectorized SearchValues to instantly reject invalid chars (garbage, symbols)
        if (s.IndexOfAnyExcept(InputBase32Chars) >= 0) {
            result = default;
            return false;
        }

        // Padding validation logic
        // Padding '=' must only appear at the end.
        // We find the index of the first padding char.
        int paddingIndex = s.IndexOf('=');
        if (paddingIndex >= 0) {
            // If padding exists, everything after it MUST also be padding.
            ReadOnlySpan<char> tail = s[paddingIndex..];
            foreach (char c in tail) {
                if (c != '=') {
                    result = default;
                    return false;
                }
            }
        }

        // Strict RFC 4648 length check (optional but recommended)
        if (s.Length % 8 != 0) {
            result = default;
            return false;
        }

        // Optimization 2: Check if we need to convert to UpperCase.
        // If not, we can avoid allocating a new string.
        if (s.IndexOfAny(LowerCaseLetters) < 0) {
            result = new Base32String(s.ToString());
            return true;
        }

        // Create normalized uppercase string
        result = new Base32String(s.ToString().ToUpperInvariant());
        return true;
    }

    /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.Parse(ReadOnlySpan{byte}, IFormatProvider?)"/>
    public static Base32String Parse(ReadOnlySpan<byte> utf8Text) {
        if (TryParse(utf8Text, out Base32String result))
            return result;
        throw new FormatException("The input is not a valid Base32 string.");
    }

    /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.TryParse(ReadOnlySpan{byte}, IFormatProvider?, out TSelf)"/>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Base32String result) {
        if (utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        // Optimization: Validate bytes directly without converting to string first
        if (utf8Text.IndexOfAnyExcept(InputBase32Bytes) >= 0) {
            result = default;
            return false;
        }

        // For structural validation (padding, etc.), we defer to the Char implementation 
        // by converting to string. Since we already validated the bytes are safe ASCII/Base32,
        // this is safe, though slightly allocaty. 
        // (Further optimization could be done here to validate structure on bytes directly if needed).
        return TryParse(Encoding.UTF8.GetString(utf8Text).AsSpan(), out result);
    }

    #endregion

    #region Decoding (To Bytes)

    /// <summary>
    /// Decodes the Base32 string into a newly allocated byte array.
    /// </summary>
    /// <returns>A byte array containing the decoded binary data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBytes() {
        if (this.Value.Length == 0)
            return [];
        byte[] bytes = new byte[GetDecodedLength()];
        TryDecode(bytes, out _);
        return bytes;
    }

    /// <summary>
    /// Attempts to decode the Base32 string into the provided destination span of bytes.
    /// </summary>
    /// <param name="destination">The buffer to write the decoded bytes to.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        ReadOnlySpan<char> input = this.Value.AsSpan();

        // Count padding to determine actual data length
        int padding = 0;
        for (int i = input.Length - 1; i >= 0; i--) {
            if (input[i] == '=')
                padding++;
            else
                break;
        }

        int unpaddedLength = input.Length - padding;
        int outputLength = unpaddedLength * 5 / 8;

        if (destination.Length < outputLength) {
            bytesWritten = 0;
            return false;
        }

        long bitBuffer = 0; // Use long to prevent overflow easily
        int bitCount = 0;
        int outputIndex = 0;

        for (int i = 0; i < unpaddedLength; i++) {
            char c = input[i];
            int val = DecodeTable[c];

            // Shift 5 bits in
            bitBuffer = (bitBuffer << 5) | (uint)val;
            bitCount += 5;

            if (bitCount >= 8) {
                destination[outputIndex++] = (byte)((bitBuffer >> (bitCount - 8)) & 0xFF);
                bitCount -= 8;
            }
        }

        bytesWritten = outputIndex;
        return true;
    }

    /// <summary>
    /// Gets the exact number of bytes that the decoded Base32 string represents.
    /// </summary>
    /// <returns>The number of bytes.</returns>
    public int GetDecodedLength() {
        if (this.Value.Length == 0)
            return 0;
        int padding = 0;
        for (int i = this.Value.Length - 1; i >= 0; i--) {
            if (this.Value[i] == '=')
                padding++;
            else
                break;
        }
        return (this.Value.Length - padding) * 5 / 8;
    }

    #endregion

    #region Formatting, Equality and Operators

    /// <summary>
    /// Writes the UTF-8 representation of the Base32 string to the provided buffer writer.
    /// </summary>
    public void WriteTo(IBufferWriter<byte> writer) {
        if (string.IsNullOrEmpty(_encodedValue))
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
        return this.Value.GetHashCode();
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

/// <summary>
/// A custom JsonConverter for serializing and deserializing the <see cref="Base32String"/> struct.
/// </summary>
public sealed class Base32StringJsonConverter : JsonConverter<Base32String> {

    /// <inheritdoc/>
    public override Base32String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            // Try to parse directly from UTF8 bytes if possible for performance
            if (reader.HasValueSequence) {
                // Sequence handling is complex, fallback to string
                return Base32String.Parse(reader.GetString()!);
            }
            else {
                return Base32String.Parse(reader.ValueSpan);
            }
        }

        string? value = reader.GetString();
        return value is null ? default : Base32String.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base32String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}