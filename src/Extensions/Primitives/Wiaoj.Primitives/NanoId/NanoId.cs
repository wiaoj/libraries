using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a URL-friendly, unique string identifier (NanoID).
/// Optimized for high-performance generation using stack allocation, bitwise masking, and SIMD validation.
/// <para>
/// Standard Alphabet: A-Za-z0-9_- (64 chars)
/// Default Length: 21 chars (~126 bits of entropy)
/// </para>
/// </summary>
[DebuggerDisplay("{Value}")]
[TypeConverter(typeof(NanoIdTypeConverter))]
[JsonConverter(typeof(NanoIdJsonConverter))]
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public readonly partial record struct NanoId :
    IEquatable<NanoId>,
    IComparable<NanoId>,
    IComparable,
    ISpanParsable<NanoId>,    // Explicit
    ISpanFormattable          // Explicit
{
    // -------------------------------------------------------------------------
    // CONSTANTS & CONFIG
    // -------------------------------------------------------------------------
    //private const string DefaultAlphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-";
    private const int DefaultLength = 21;
    internal const int MaxAllowedLength = 128;

    // SIMD Optimized Validator (.NET 8+)
    private static readonly SearchValues<char> ValidChars = SearchValues.Create(Alphabets.UrlSafe);

    private static ReadOnlySpan<char> Alphabet => Alphabets.NoVowels;

    private readonly string _value;

    /// <summary>Represents an empty NanoId.</summary>
    public static NanoId Empty { get; } = new(string.Empty);

    /// <summary>Gets the string value of the NanoId.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Returns true if the NanoId is empty.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    private NanoId(string value) => _value = value;

    // -------------------------------------------------------------------------
    // GENERATION (High Performance)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a new NanoId using the default alphabet and length (21).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NanoId NewId() => NewId(DefaultLength);

    /// <summary>
    /// Generates a new NanoId with a custom length using the default alphabet.
    /// <para>Uses Bitwise Masking (fastest) since default alphabet size is 64.</para>
    /// </summary>
    public static NanoId NewId(int length) {
        Preca.ThrowIfNonValidNanoIdLength(length);

        //string result = string.Create(length, length, (span, len) => {
        //    // 1. Stack Allocation
        //    Span<byte> randomBytes = stackalloc byte[len];

        //    // 2. Secure Randomness
        //    RandomNumberGenerator.Fill(randomBytes);

        //    // 3. Bitwise Optimization (Alphabet size 64 = 2^6)
        //    ReadOnlySpan<char> alphabet = Alphabet;
        //    for (int i = 0; i < len; i++) {
        //        span[i] = alphabet[randomBytes[i] & 0x3F];
        //    }
        //});

        string result = string.Create(length, length, (span, len) => {
            RandomNumberGenerator.GetItems(Alphabet, span);
        });

        return new NanoId(result);
    }

    /// <summary>
    /// Generates a new NanoId using a CUSTOM alphabet and length.
    /// <para>
    /// NOTE: The custom alphabet must be a subset of the standard URL-safe characters (A-Za-z0-9_-).
    /// This ensures the resulting ID is always validatable by <see cref="Parse"/>.
    /// </para>
    /// </summary>
    /// <param name="customAlphabet">The custom characters to use (e.g. "0123456789").</param>
    /// <param name="length">The length of the ID.</param>
    public static NanoId NewId(string customAlphabet, int length) {
        Preca.ThrowIfNonValidNanoIdLength(length);
        Preca.ThrowIfZero(customAlphabet.Length, static () => new ArgumentException("Alphabet cannot be empty."));
        Preca.ThrowIfGreaterThanOrEqualTo(
            customAlphabet.IndexOfAnyExcept(ValidChars), 
            0, 
            () => new ArgumentException("Custom alphabet contains invalid characters. Only URL-safe characters (A-Za-z0-9_-) are allowed to maintain strict typing."));

        string result = string.Create(length, customAlphabet, (span, alphabet) => {  
            RandomNumberGenerator.GetItems(alphabet.AsSpan(), span);
        });           
        return new NanoId(result);
    }  

    // -------------------------------------------------------------------------
    // PARSING
    // -------------------------------------------------------------------------

    public static NanoId Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    public static NanoId Parse(ReadOnlySpan<char> s) {
        if (TryParseInternal(s, out NanoId result))
            return result;
        throw new FormatException($"Invalid NanoId format. Contains illegal characters or invalid length.");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out NanoId result) {
        if (s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out NanoId result) {
        return TryParseInternal(s, out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out NanoId result) {
        if (s.IsEmpty || s.Length > MaxAllowedLength) {
            result = default;
            return false;
        }

        // SIMD Check
        if (s.IndexOfAnyExcept(ValidChars) >= 0) {
            result = default;
            return false;
        }

        result = new NanoId(s.ToString());
        return true;
    }

    // -------------------------------------------------------------------------
    // FORMATTING
    // -------------------------------------------------------------------------

    public override string ToString() => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if (string.IsNullOrEmpty(_value)) {
            charsWritten = 0;
            return false;
        }

        if (destination.Length < _value.Length) {
            charsWritten = 0;
            return false;
        }

        _value.CopyTo(destination);
        charsWritten = _value.Length;
        return true;
    }

    // -------------------------------------------------------------------------
    // EXPLICIT INTERFACES
    // -------------------------------------------------------------------------

    static NanoId IParsable<NanoId>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<NanoId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out NanoId result) => TryParse(s, out result);
    static NanoId ISpanParsable<NanoId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<NanoId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out NanoId result) => TryParse(s, out result);

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => TryFormat(destination, out charsWritten);
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => Value;

    // -------------------------------------------------------------------------
    // EQUALITY & OPERATORS
    // -------------------------------------------------------------------------

    public bool Equals(NanoId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public int CompareTo(NanoId other) => string.CompareOrdinal(_value, other._value);
    public int CompareTo(object? obj) => obj is NanoId other ? CompareTo(other) : 1;

    public static implicit operator string(NanoId id) => id.Value;
    public static explicit operator NanoId(string s) => Parse(s);
}

// -------------------------------------------------------------------------
// CONVERTERS
// -------------------------------------------------------------------------

public static class PrecaExtensions {
    extension(Preca) {
        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified NanoId length is not valid.
        /// Valid lengths are between 1 and <see cref="NanoId.MaxAllowedLength"/>.
        /// </summary>
        /// <param name="length">The NanoId length to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is less than 1 or greater than <see cref="NanoId.MaxAllowedLength"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNonValidNanoIdLength(int length) {
            if(length <= 0 || length > NanoId.MaxAllowedLength) {
                throw new ArgumentOutOfRangeException(nameof(length), $"Length must be between 1 and {NanoId.MaxAllowedLength}.");
            }
        }
    }
}

public class NanoIdJsonConverter : JsonConverter<NanoId> {
    public override NanoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            if (reader.HasValueSequence)
                return NanoId.Parse(reader.GetString()!);
            return NanoId.Parse(reader.GetString()!);
        }
        return NanoId.Empty;
    }

    public override void Write(Utf8JsonWriter writer, NanoId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    public override NanoId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => NanoId.Parse(reader.GetString()!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, NanoId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}

public class NanoIdTypeConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if (value is string str)
            return NanoId.Parse(str);
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        if (value is NanoId id && destinationType == typeof(string))
            return id.ToString();
        return base.ConvertTo(context, culture, value, destinationType);
    }
}