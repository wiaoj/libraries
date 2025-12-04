using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a string containing only hexadecimal characters ([0-9a-fA-F]).
/// </summary>
/// <remarks>
/// This value object fights "primitive obsession" by ensuring that any instance of <see cref="HexString"/>
/// holds a structurally valid hexadecimal string. The implementation is self-contained, using high-performance,
/// low-allocation techniques that do not rely on specific .NET version APIs for core functionality.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(HexStringJsonConverter))]
public readonly record struct HexString {
    private readonly string _value;

    /// <summary>
    /// Represents an empty hexadecimal string.
    /// </summary>
    public static HexString Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the underlying hexadecimal string value.
    /// Returns an empty string if the structure was created with its default constructor.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    // The constructor is private to ensure all creation goes through validation factory methods.
    private HexString(string validatedValue) {
        this._value = validatedValue;
    }

    #region Creation (From Bytes)

    /// <summary>
    /// Encodes a span of bytes into a hexadecimal string using a high-performance, low-allocation method.
    /// </summary>
    /// <param name="bytes">The raw bytes to encode.</param>
    /// <returns>A new <see cref="HexString"/> instance containing the encoded data.</returns>
    public static HexString FromBytes(ReadOnlySpan<byte> bytes) {
        if (bytes.IsEmpty) return Empty;

        // Use string.Create for an allocation-free conversion.
        return new HexString(string.Create(bytes.Length * 2, bytes, (chars, state) => {
            for (int i = 0; i < state.Length; i++) {
                byte b = state[i];
                chars[i * 2] = ToHexChar(b >> 4);      // High nibble
                chars[i * 2 + 1] = ToHexChar(b & 0x0F); // Low nibble
            }
        }));
    }

    #endregion

    #region Parsing (From Text)

    /// <summary>
    /// Parses a string into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A new <see cref="HexString"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The input string is not a valid hexadecimal string.</exception>
    public static HexString Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if (TryParse(s.AsSpan(), out HexString result)) return result;
        throw new FormatException("The input string is not a valid hexadecimal string.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="HexString"/> if parsing succeeded, or a default value if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out HexString result) {
        if (s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Parses a character span into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A new <see cref="HexString"/> instance.</returns>
    /// <exception cref="FormatException">The input is not a valid hexadecimal string.</exception>
    public static HexString Parse(ReadOnlySpan<char> s) {
        if (TryParse(s, out HexString result)) return result;
        throw new FormatException("The input is not a valid hexadecimal string.");
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="HexString"/> if parsing succeeded, or a default value if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out HexString result) {
        if (s.Length % 2 != 0) {
            result = default;
            return false;
        }
        if (s.IsEmpty) {
            result = Empty;
            return true;
        }

        foreach (char c in s) {
            if (!IsAsciiHexDigit(c)) {
                result = default;
                return false;
            }
        }

        result = new HexString(s.ToString());
        return true;
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A new <see cref="HexString"/> instance.</returns>
    /// <exception cref="FormatException">The input is not a valid hexadecimal string.</exception>
    public static HexString Parse(ReadOnlySpan<byte> utf8Text) {
        if (TryParse(utf8Text, out HexString result)) return result;
        throw new FormatException("The input is not a valid hexadecimal string.");
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="HexString"/> if parsing succeeded, or a default value if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="utf8Text"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out HexString result) {
        if (utf8Text.Length % 2 != 0) {
            result = default;
            return false;
        }
        if (utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        foreach (byte b in utf8Text) {
            if (!IsAsciiHexDigit(b)) {
                result = default;
                return false;
            }
        }

        result = new HexString(Encoding.UTF8.GetString(utf8Text));
        return true;
    }

    #endregion

    #region Decoding (To Bytes)

    /// <summary>
    /// Decodes the hexadecimal string into a newly allocated byte array.
    /// </summary>
    /// <returns>A new byte array containing the decoded data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBytes() {
        if (this.Value.Length == 0) {
            return Array.Empty<byte>();
        }
        byte[] bytes = new byte[GetDecodedLength()];
        TryDecode(bytes, out _); // This will always succeed for a valid HexString instance.
        return bytes;
    }

    /// <summary>
    /// Attempts to decode the hexadecimal string into the provided destination span of bytes.
    /// </summary>
    /// <param name="destination">The buffer to receive the decoded bytes.</param>
    /// <param name="bytesWritten">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if decoding was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        int requiredLength = GetDecodedLength();
        if (destination.Length < requiredLength) {
            bytesWritten = 0;
            return false;
        }

        ReadOnlySpan<char> source = this.Value.AsSpan();

        for (int i = 0; i < requiredLength; i++) {
            int hi = HexCharToValue(source[i * 2]);
            int lo = HexCharToValue(source[i * 2 + 1]);
            destination[i] = (byte)((hi << 4) | lo);
        }

        bytesWritten = requiredLength;
        return true;
    }

    /// <summary>
    /// Gets the exact number of bytes that the decoded hexadecimal string represents.
    /// </summary>
    /// <returns>The length of the decoded data in bytes.</returns>
    public int GetDecodedLength() {
        return this.Value.Length / 2;
    }

    #endregion

    /// <summary>
    /// Returns a new HexString with all alphabetic characters converted to uppercase.
    /// </summary>
    /// <returns>
    /// A new <see cref="HexString"/> in uppercase. Returns the current instance if it's already in the correct format.
    /// </returns>
    public HexString ToUpper() { 
        bool needsConversion = false;
        foreach (char c in this.Value) {
            if (c >= 'a' && c <= 'f') {
                needsConversion = true;
                break;
            }
        }

        if (!needsConversion) {
            return this;
        }
         
        return new HexString(string.Create(this.Value.Length, this.Value, (destination, source) => {
            for (int i = 0; i < source.Length; i++) {
                char c = source[i];
                destination[i] = (c >= 'a' && c <= 'f') ? (char)(c - 32) : c;
            }
        }));
    }

    /// <summary>
    /// Returns a new HexString with all alphabetic characters converted to lowercase.
    /// </summary>
    /// <returns>
    /// A new <see cref="HexString"/> in lowercase. Returns the current instance if it's already in the correct format.
    /// </returns>
    public HexString ToLower() { 
        bool needsConversion = false;
        foreach (char c in this.Value) {
            if (c >= 'A' && c <= 'F') {
                needsConversion = true;
                break;
            }
        }

        if (!needsConversion) {
            return this;
        }

        // string.Create ile allocation-free dönüşüm
        return new HexString(string.Create(this.Value.Length, this.Value, (destination, source) => {
            for (int i = 0; i < source.Length; i++) {
                char c = source[i];
                destination[i] = (c >= 'A' && c <= 'F') ? (char)(c + 32) : c;
            }
        }));
    }

    #region Helpers, Formatting, Equality & Operators

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    // `record struct` handles Equals and GetHashCode automatically.

    /// <summary>
    /// Implicitly converts a <see cref="HexString"/> to its underlying <see cref="string"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(HexString s) {
        return s.Value;
    }

    /// <summary>
    /// Implicitly converts a <see cref="HexString"/> to a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(HexString s) {
        return s.Value.AsSpan();
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="HexString"/>.
    /// </summary>
    /// <exception cref="FormatException">The provided string is not in a valid hexadecimal format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator HexString(string s) {
        return Parse(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ToHexChar(int value) {
        return (char)(value < 10 ? '0' + value : 'a' + value - 10);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexCharToValue(char c) {
        if (c is >= '0' and <= '9') return c - '0';
        if (c is >= 'a' and <= 'f') return c - 'a' + 10;
        if (c is >= 'A' and <= 'F') return c - 'A' + 10;
        return -1; // Should not happen for a valid HexString.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiHexDigit(char c) {
        return c is >= '0' and <= '9' or
        >= 'a' and <= 'f' or
        >= 'A' and <= 'F';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiHexDigit(byte b) {
        return b is >= (byte)'0' and <= (byte)'9' or
        >= (byte)'a' and <= (byte)'f' or
        >= (byte)'A' and <= (byte)'F';
    }

    #endregion
}

/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing the <see cref="HexString"/> struct.
/// </summary>
public sealed class HexStringJsonConverter : JsonConverter<HexString> {
    /// <inheritdoc/>
    public override HexString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            // Optimize by parsing the raw UTF-8 bytes directly from the reader to avoid string allocation.
            ReadOnlySpan<byte> utf8Value = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
            return HexString.Parse(utf8Value);
        }

        // Fallback for non-string or null tokens.
        string? value = reader.GetString();
        return value is null ? default : HexString.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, HexString value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}