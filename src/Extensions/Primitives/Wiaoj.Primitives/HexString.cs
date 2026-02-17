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
/// holds a structurally valid hexadecimal string. The implementation uses .NET 8's <see cref="SearchValues{T}"/> 
/// for vectorized (SIMD) validation and high-performance parsing.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(HexStringJsonConverter))]
public readonly record struct HexString {

    // Optimized search sets for validation and case checking.
    private static readonly SearchValues<char> HexChars = SearchValues.Create("0123456789abcdefABCDEF");
    private static readonly SearchValues<byte> HexBytes = SearchValues.Create("0123456789abcdefABCDEF"u8);
    private static readonly SearchValues<char> LowerHexLetters = SearchValues.Create("abcdef");
    private static readonly SearchValues<char> UpperHexLetters = SearchValues.Create("ABCDEF");

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

    #region Creation 

    /// <summary>
    /// Encodes a span of bytes into a hexadecimal string using a high-performance, low-allocation method.
    /// </summary>
    /// <param name="bytes">The raw bytes to encode.</param>
    /// <returns>A new <see cref="HexString"/> instance containing the encoded data.</returns>
    [SkipLocalsInit]
    public static HexString FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty)
            return Empty;

        // string.Create allows us to write directly into the string memory, avoiding intermediate allocations.
        return new HexString(string.Create(bytes.Length * 2, bytes, (chars, source) => {
            Convert.TryToHexString(source, chars, out _);
        }));
    }

    /// <summary>
    /// Encodes a plain UTF-8 string into a HexString.
    /// Example: FromUtf8("hello") -> "68656c6c6f"
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>A hex string representing the UTF-8 bytes of the input text.</returns>
    public static HexString FromUtf8(string text) {
        if(string.IsNullOrEmpty(text))
            return Empty;
        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Encodes a string using the specified encoding into a HexString.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns>A hex string representing the bytes of the input text.</returns>
    public static HexString From(string text, Encoding encoding) {
        if(string.IsNullOrEmpty(text))
            return Empty;
        ArgumentNullException.ThrowIfNull(encoding);
        return FromBytes(encoding.GetBytes(text));
    }

    #endregion

    #region Parsing (From Text)

    /// <summary>
    /// Writes the UTF-8 representation of the Hex string to the provided buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    public void WriteTo(IBufferWriter<byte> writer) {
        if(string.IsNullOrEmpty(this._value))
            return;

        ReadOnlySpan<char> chars = this._value.AsSpan();
        // Hex string contains only ASCII, so byte count == char count
        int byteCount = chars.Length;

        Span<byte> buffer = writer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(chars, buffer);
        writer.Advance(byteCount);
    }

    /// <summary>
    /// Parses a string into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A new <see cref="HexString"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">The input string is not a valid hexadecimal string.</exception>
    public static HexString Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if(TryParse(s.AsSpan(), out HexString result))
            return result;
        throw new FormatException("The input string is not a valid hexadecimal string.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="HexString"/> if parsing succeeded, or a default value if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out HexString result) {
        if(s is null) {
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
        if(TryParse(s, out HexString result))
            return result;
        throw new FormatException("The input is not a valid hexadecimal string.");
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="HexString"/> if parsing succeeded, or a default value if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out HexString result) {
        if(s.Length % 2 != 0) {
            result = default;
            return false;
        }
        if(s.IsEmpty) {
            result = Empty;
            return true;
        }

        // Optimized check: Uses vectorized instructions to find any invalid char instantly.
        if(s.IndexOfAnyExcept(HexChars) >= 0) {
            result = default;
            return false;
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
        if(TryParse(utf8Text, out HexString result))
            return result;
        throw new FormatException("The input is not a valid hexadecimal string.");
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="HexString"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="HexString"/> if parsing succeeded, or a default value if it failed.</param>
    /// <returns><see langword="true"/> if <paramref name="utf8Text"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out HexString result) {
        if(utf8Text.Length % 2 != 0) {
            result = default;
            return false;
        }
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        // Optimized check: Uses vectorized instructions to find any invalid byte instantly.
        // Replaces the older foreach loop.
        if(utf8Text.IndexOfAnyExcept(HexBytes) >= 0) {
            result = default;
            return false;
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
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToBytes() {
        if(this.Value.Length == 0) {
            return [];
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
        //int requiredLength = GetDecodedLength();
        //if (destination.Length < requiredLength) {
        //    bytesWritten = 0;
        //    return false;
        //} 
        //ReadOnlySpan<char> source = this.Value.AsSpan();

        //for (int i = 0; i < requiredLength; i++) {
        //    int hi = HexCharToValue(source[i * 2]);
        //    int lo = HexCharToValue(source[i * 2 + 1]);
        //    destination[i] = (byte)((hi << 4) | lo);
        //}

        //bytesWritten = requiredLength;
        //return true;

        int requiredLength = GetDecodedLength();
        if(destination.Length < requiredLength) {
            bytesWritten = 0;
            return false;
        }

        ReadOnlySpan<char> source = this.Value.AsSpan();

        for(int i = 0; i < requiredLength; i++) {
            // İki karakteri de alıp kontrol ediyoruz
            int hi = HexCharToValue(source[i * 2]);
            int lo = HexCharToValue(source[i * 2 + 1]);

            // Eğer karakterlerden biri bile -1 dönerse (geçersiz hex)
            if((hi | lo) < 0) {
                bytesWritten = 0;
                return false;
            }

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

    #region Case Conversion

    /// <summary>
    /// Returns a new HexString with all alphabetic characters converted to uppercase.
    /// </summary>
    /// <returns>
    /// A new <see cref="HexString"/> in uppercase. Returns the current instance if it's already in the correct format.
    /// </returns>
    public HexString ToUpper() {
        // Optimization: Quick check using SearchValues to see if any work is needed.
        if(this.Value.AsSpan().IndexOfAny(LowerHexLetters) < 0) {
            return this;
        }

        return new HexString(string.Create(this.Value.Length, this.Value, (destination, source) => {
            for(int i = 0; i < source.Length; i++) {
                char c = source[i];
                // Convert 'a'-'f' to 'A'-'F' efficiently.
                destination[i] = (c is >= 'a' and <= 'f') ? (char)(c - 32) : c;
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
        // Optimization: Quick check using SearchValues to see if any work is needed.
        if(this.Value.AsSpan().IndexOfAny(UpperHexLetters) < 0) {
            return this;
        }

        return new HexString(string.Create(this.Value.Length, this.Value, (destination, source) => {
            for(int i = 0; i < source.Length; i++) {
                char c = source[i];
                // Convert 'A'-'F' to 'a'-'f' efficiently.
                destination[i] = (c is >= 'A' and <= 'F') ? (char)(c + 32) : c;
            }
        }));
    }

    #endregion

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
    private static int HexCharToValue(char c) {
        int val = c;
        if(val is >= '0' and <= '9') return val - '0';
        if(val is >= 'a' and <= 'f') return val - 'a' + 10;
        if(val is >= 'A' and <= 'F') return val - 'A' + 10;
        return -1;
    }

    #endregion
}

/// <summary>
/// A custom <see cref="JsonConverter"/> for serializing and deserializing the <see cref="HexString"/> struct.
/// </summary>
public sealed class HexStringJsonConverter : JsonConverter<HexString> {
    /// <inheritdoc/>
    public override HexString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            if(reader.HasValueSequence) {
                long len = reader.ValueSequence.Length;
                if(len <= 256) {
                    Span<byte> stackSpan = stackalloc byte[(int)len];
                    reader.ValueSequence.CopyTo(stackSpan);
                    return HexString.Parse(stackSpan);
                }
                else {
                    byte[] rented = ArrayPool<byte>.Shared.Rent((int)len);
                    try {
                        Span<byte> span = rented.AsSpan(0, (int)len);
                        reader.ValueSequence.CopyTo(span);
                        return HexString.Parse(span);
                    }
                    finally {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }

            return HexString.Parse(reader.ValueSpan);
        }

        // Fallback for non-string or null tokens.
        string? value = reader.GetString();
        return value is null ? HexString.Empty : HexString.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, HexString value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}