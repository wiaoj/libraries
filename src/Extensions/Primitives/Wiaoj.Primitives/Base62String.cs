using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a structurally valid Base62 string (alphanumeric [0-9a-zA-Z]).
/// This value object ensures that the data is validated upon creation, eliminating repeated validation checks.
/// </summary>
/// <remarks>
/// Base62 is commonly used for URL shortening and compact representation of large integers (like UUIDs or Snowflake IDs).
/// This implementation uses Big Endian byte order for byte-array encoding.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(Base62StringJsonConverter))]
public readonly record struct Base62String :
    IEquatable<Base62String>,
    ISpanParsable<Base62String>,
    ISpanFormattable,
    IUtf8SpanParsable<Base62String> {

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    // Optimized search values for .NET 8+ to speed up validation
    private static readonly SearchValues<char> Base62Chars = SearchValues.Create(Alphabet);

    // For UTF-8 byte validation
    private static readonly SearchValues<byte> Base62Utf8Bytes = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    private readonly string _value;

    /// <summary>
    /// Gets an instance representing an empty Base62 string.
    /// </summary>
    public static Base62String Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the underlying Base62 string value.
    /// Returns an empty string if the structure is default.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    // Private constructor enforces validation via factory methods
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

        // Base62 efficiency is approx log2(62)/8 = 0.74 bytes per char.
        // Inverse: 1 char ~= 5.95 bits. 
        // We calculate max length to avoid reallocation.
        int estimatedLength = (int)Math.Ceiling(bytes.Length * 8.0 / 5.954196);

        // Use BigInteger for arbitrary length byte arrays.
        // We force unsigned and big-endian to treat the byte array as a single large positive number.
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

            // Fill leading zeros if the estimated length was larger than the actual significant digits.
            // This ensures the string length matches the estimation, providing consistent padding behavior.
            while(i >= 0) {
                span[i--] = Alphabet[0];
            }
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

        // long.MaxValue (9,223,372,036,854,775,807) Base62'de "AzL8n0Y58m7" (11 hane) eder.
        // Emniyet payı ile 13 karakterlik yer ayırıyoruz.
        Span<char> buffer = stackalloc char[13];
        int idx = 12; // Buffer'ın sonundan başlıyoruz

        while(value > 0) {
            // Mod alma ve bölme işlemi
            // (int) cast işlemi güvenlidir çünkü sonuç 0-61 arasıdır.
            buffer[idx--] = Alphabet[(int)(value % 62)];
            value /= 62;
        }

        // Dolu olan kısmı alıp string'e çeviriyoruz.
        return new Base62String(buffer[(idx + 1)..].ToString());
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A validated <see cref="Base62String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 characters.</exception>
    public static Base62String Parse(string s) {
        if(TryParse(s, null, out Base62String result)) return result;
        throw new FormatException($"Invalid Base62 string: '{s}'");
    }

    /// <summary>
    /// Parses a character span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <returns>A validated <see cref="Base62String"/>.</returns>
    /// <exception cref="FormatException">Thrown if the input contains invalid Base62 characters.</exception>
    public static Base62String Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, null, out Base62String result)) return result;
        throw new FormatException("Invalid Base62 string.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (ignored).</param>
    /// <param name="result">The result if parsing succeeded.</param>
    /// <returns><see langword="true"/> if successful; otherwise <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base62String result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="provider">Format provider (ignored).</param>
    /// <param name="result">The result if parsing succeeded.</param>
    /// <returns><see langword="true"/> if successful; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base62String result) {
        if(s.IsEmpty) {
            result = Empty;
            return true;
        }

        // SIMD-accelerated validation: returns true if any char is NOT in the allowed set
        if(s.IndexOfAnyExcept(Base62Chars) >= 0) {
            result = default;
            return false;
        }

        result = new Base62String(s.ToString());
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Base62String"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 bytes to parse.</param>
    /// <param name="provider">Format provider (ignored).</param>
    /// <param name="result">The result if parsing succeeded.</param>
    /// <returns><see langword="true"/> if successful; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base62String result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        // Validate that all bytes correspond to Base62 ASCII characters
        if(utf8Text.IndexOfAnyExcept(Base62Utf8Bytes) >= 0) {
            result = default;
            return false;
        }

        // Since Base62 is ASCII subset, we can just treat bytes as chars directly 
        // or convert using Encoding.UTF8.
        result = new Base62String(Encoding.UTF8.GetString(utf8Text));
        return true;
    }

    // Explicit Interface Implementations
    static Base62String IParsable<Base62String>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static Base62String ISpanParsable<Base62String>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static Base62String IUtf8SpanParsable<Base62String>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if(TryParse(utf8Text, provider, out Base62String result)) return result;
        throw new FormatException("Invalid Base62 UTF-8 sequence.");
    }

    #endregion

    #region Decoding

    /// <summary>
    /// Decodes the Base62 string back to a long.
    /// Uses Horner's Method for high performance and accurate overflow detection.
    /// </summary>
    public long ToInt64() {
        if(string.IsNullOrEmpty(this._value)) return 0;

        long result = 0;

        // "123" -> ((1 * 10) + 2) * 10 + 3
        foreach(char c in this._value) {
            int val = CharToValue(c);

            result = checked((result * 62) + val);
        }

        return result;
    }

    /// <summary>
    /// Decodes the Base62 string to a byte array (Big Endian).
    /// </summary>
    /// <returns>A new byte array containing the decoded value.</returns>
    public byte[] ToBytes() {
        if(string.IsNullOrEmpty(this._value)) return [];

        // Decode to BigInteger first
        BigInteger result = BigInteger.Zero;
        BigInteger multiplier = BigInteger.One;
        BigInteger base62 = new(62);

        for(int i = this._value.Length - 1; i >= 0; i--) {
            int val = CharToValue(this._value[i]);
            result += val * multiplier;
            multiplier *= base62;
        }

        // Convert BigInteger to byte array (unsigned, big endian)
        return result.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CharToValue(char c) {
        if(c is >= '0' and <= '9') return c - '0';
        if(c is >= 'A' and <= 'Z') return c - 'A' + 10;
        if(c is >= 'a' and <= 'z') return c - 'a' + 36;
        // Should not happen if instance was created via Parse/Tryparse
        throw new FormatException($"Invalid Base62 character: {c}");
    }

    #endregion

    #region Formatting & Overrides

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this._value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this._value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this.Value.GetHashCode();
    }

    /// <inheritdoc/>
    public bool Equals(Base62String other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicitly converts a <see cref="Base62String"/> to a <see cref="string"/>.
    /// </summary>
    public static implicit operator string(Base62String s) {
        return s.Value;
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="Base62String"/>.
    /// </summary>
    public static explicit operator Base62String(string s) {
        return Parse(s);
    }

    /// <summary>
    /// Explicitly converts a long to a <see cref="Base62String"/>.
    /// </summary>
    public static explicit operator Base62String(long l) {
        return FromInt64(l);
    }

    #endregion
}

/// <summary>
/// A custom JsonConverter for serializing and deserializing <see cref="Base62String"/>.
/// </summary>
public sealed class Base62StringJsonConverter : JsonConverter<Base62String> {

    /// <inheritdoc/>
    public override Base62String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            // Try to read directly from UTF-8 span for performance
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            if(Base62String.TryParse(span, null, out Base62String result)) {
                return result;
            }

            // If the string is invalid Base62, we should throw to inform the deserializer
            throw new JsonException($"The value '{reader.GetString()}' is not a valid Base62 string.");
        }

        throw new JsonException($"Unexpected token type '{reader.TokenType}' for Base62String.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Base62String value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}