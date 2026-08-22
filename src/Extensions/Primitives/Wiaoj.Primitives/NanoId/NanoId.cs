using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
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
[DebuggerDisplay("{ToString(),nq}")]
[TypeConverter(typeof(NanoIdTypeConverter))]
[JsonConverter(typeof(NanoIdJsonConverter))]
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public readonly partial record struct NanoId :
    IEquatable<NanoId>,
    IComparable<NanoId>,
    IComparable,
    IParsable<NanoId>,
    ISpanParsable<NanoId>,
    IUtf8SpanParsable<NanoId>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<NanoId, NanoId, bool>
{
    // -------------------------------------------------------------------------
    // CONSTANTS & CONFIG
    // -------------------------------------------------------------------------

    /// <summary>
    /// The default length for a generated <see cref="NanoId"/>.
    /// </summary>
    private const int DefaultLength = 21;

    /// <summary>
    /// The maximum permitted length for a <see cref="NanoId"/> to prevent excessive memory allocation or denial-of-service attacks.
    /// </summary>
    public const int MaxAllowedLength = 128;

    /// <summary>
    /// SIMD-optimized search set containing valid characters for the standard URL-safe NanoId.
    /// </summary>
    private static readonly SearchValues<char> ValidChars = SearchValues.Create(Alphabets.UrlSafe);

    /// <summary>
    /// Gets the internal alphabet used for generation (defaults to NoVowels to prevent profanity).
    /// </summary>
    private static ReadOnlySpan<char> Alphabet => Alphabets.NoVowels;

    /// <summary>
    /// The underlying string value of the identifier.
    /// </summary>
    private readonly string _value;

    /// <summary>
    /// Gets a <see cref="NanoId"/> that represents an empty value.
    /// </summary>
    public static NanoId Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the string representation of this <see cref="NanoId"/>.
    /// Returns an empty string if the identifier is not initialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the current <see cref="NanoId"/> is empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoId"/> struct with a validated value.
    /// </summary>
    /// <param name="value">The validated string value.</param>
    private NanoId(string value) {
        this._value = value;
    }

    // -------------------------------------------------------------------------
    // GENERATION (High Performance)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a new cryptographically secure <see cref="NanoId"/> using the default length (21) 
    /// and the profanity-safe alphabet.
    /// </summary>
    /// <returns>A new, unique <see cref="NanoId"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NanoId NewId() {
        return NewId(DefaultLength);
    }

    /// <summary>
    /// Generates a new cryptographically secure <see cref="NanoId"/> with the specified length 
    /// using the profanity-safe alphabet.
    /// </summary>
    /// <param name="length">The desired length of the identifier. Must be between 1 and <see cref="MaxAllowedLength"/>.</param>
    /// <returns>A new <see cref="NanoId"/> with the specified length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is less than or equal to zero or exceeds <see cref="MaxAllowedLength"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NanoId NewId(int length) {
        Preca.ThrowIfNonValidNanoIdLength(length);

        string result = string.Create(length, length, (span, len) => {
            RandomNumberGenerator.GetItems(Alphabet, span);
        });

        return new NanoId(result);
    }

    /// <summary>
    /// Generates a new cryptographically secure <see cref="NanoId"/> using a custom alphabet and length.
    /// </summary>
    /// <remarks>
    /// NOTE: The custom alphabet must be a subset of the standard URL-safe characters (A-Za-z0-9_-) 
    /// to maintain strict validation rules.
    /// </remarks>
    /// <param name="customAlphabet">The set of characters to use for generation.</param>
    /// <param name="length">The desired length of the identifier.</param>
    /// <returns>A new <see cref="NanoId"/> generated from the custom alphabet.</returns>
    /// <exception cref="ArgumentException">Thrown when the alphabet is empty or contains invalid (non URL-safe) characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is invalid.</exception>
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

    /// <summary>
    /// Generates a new cryptographically secure NanoId directly into the destination span without heap allocation using default length.
    /// </summary>
    public static bool TryGenerate(Span<char> destination) => TryGenerate(destination, DefaultLength);

    /// <summary>
    /// Generates a new cryptographically secure NanoId directly into the destination span without heap allocation.
    /// </summary>
    public static bool TryGenerate(Span<char> destination, int length) {
        if(length is <= 0 or > MaxAllowedLength) return false;
        if(destination.Length < length) return false;

        RandomNumberGenerator.GetItems(Alphabet, destination[..length]);
        return true;
    }

    // -------------------------------------------------------------------------
    // PARSING
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a string representation of a <see cref="NanoId"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The parsed <see cref="NanoId"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the string contains illegal characters or has an invalid length.</exception>
    public static NanoId Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="NanoId"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <returns>The parsed <see cref="NanoId"/>.</returns>
    /// <exception cref="FormatException">Thrown when the input is invalid.</exception>
    public static NanoId Parse(ReadOnlySpan<char> s) {
        if(TryParseInternal(s, out NanoId result))
            return result;
        throw new FormatException("Invalid NanoId format. Contains illegal characters or invalid length.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="NanoId"/>.
    /// </summary>
    public static NanoId Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out NanoId result))
            return result;
        throw new FormatException("Invalid UTF-8 sequence for NanoId.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="NanoId"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out NanoId result) {
        if(s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="NanoId"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out NanoId result) {
        return TryParseInternal(s, out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="NanoId"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out NanoId result) {
        if(utf8Text.IsEmpty || utf8Text.Length > MaxAllowedLength) {
            result = default;
            return false;
        }

        Span<char> chars = stackalloc char[utf8Text.Length];
        if(System.Text.Encoding.UTF8.GetChars(utf8Text, chars) == utf8Text.Length) {
            return TryParseInternal(chars, out result);
        }

        result = default;
        return false;
    }

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static NanoId IParsable<NanoId>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<NanoId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out NanoId result) => TryParse(s, out result);
    static NanoId ISpanParsable<NanoId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<NanoId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out NanoId result) => TryParse(s, out result);
    static NanoId IUtf8SpanParsable<NanoId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<NanoId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out NanoId result) => TryParse(utf8Text, out result);

    #endregion

    /// <summary>
    /// Internal method to validate and parse the input character span using SIMD-optimized checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out NanoId result) {
        if(s.IsEmpty || s.Length > MaxAllowedLength) {
            result = default;
            return false;
        }

        // SIMD Check
        if(s.IndexOfAnyExcept(ValidChars) >= 0) {
            result = default;
            return false;
        }

        result = new NanoId(s.ToString());
        return true;
    }

    /// <summary>
    /// Writes the NanoId value as UTF-8 bytes into the destination span.
    /// </summary>
    public bool TryWriteUtf8(Span<byte> destination, out int bytesWritten) {
        bytesWritten = 0;
        if(string.IsNullOrEmpty(this._value)) return true;
        if(destination.Length < this._value.Length) return false;

        System.Text.Unicode.Utf8.FromUtf16(this._value, destination, out _, out bytesWritten);
        return true;
    }
    
    /// <summary>
     /// Returns a ReadOnlySpan representation of the identifier.
     /// </summary>
    public ReadOnlySpan<char> AsSpan() => this._value.AsSpan();

    /// <summary>
    /// Allows the NanoId to be used in 'fixed' statements.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref readonly char GetPinnableReference() => ref this._value.GetPinnableReference();

    /// <summary>
    /// Writes the NanoId directly to the provided buffer writer.
    /// </summary>
    public void WriteTo(IBufferWriter<char> writer) {
        if(string.IsNullOrEmpty(this._value)) return;
        Span<char> span = writer.GetSpan(this._value.Length);
        this._value.CopyTo(span);
        writer.Advance(this._value.Length);
    }

    // -------------------------------------------------------------------------
    // FORMATTING
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a URN using this NanoId and a specified namespace.
    /// <para>Example: <c>ToUrn("session") -> urn:session:V1StGXR8_Z5jdHi6B-myT</c></para>
    /// </summary>
    public Urn ToUrn(string nid) {
        Preca.ThrowIf(this.IsEmpty, () => new InvalidOperationException("Cannot create URN from an empty NanoId."));
        return Urn.Create(nid, this.Value);
    }

    /// <summary>
    /// Returns the string value of the <see cref="NanoId"/>.
    /// </summary>
    /// <returns>The underlying string identifier.</returns>
    public override string ToString() => this.Value;

    /// <summary>
    /// Returns the string value using the specified format.
    /// </summary>
    public string ToString(string? format) => this.Value;

    /// <summary>
    /// Returns the string value using the specified format and format provider.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided character span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) {
            charsWritten = 0;
            return false;
        }

        if(destination.Length < this._value.Length) {
            charsWritten = 0;
            return false;
        }

        this._value.CopyTo(destination);
        charsWritten = this._value.Length;
        return true;
    }

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided UTF-8 byte span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = System.Text.Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Destination);
        return true;
    }

    // -------------------------------------------------------------------------
    // EQUALITY & OPERATORS
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool Equals(NanoId other) => string.Equals(this._value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => this._value?.GetHashCode() ?? 0;

    /// <inheritdoc/>
    public int CompareTo(NanoId other) => string.CompareOrdinal(this._value, other._value);

    /// <inheritdoc/>
    public int CompareTo(object? obj) => obj is NanoId other ? CompareTo(other) : 1;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(NanoId left, NanoId right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(NanoId left, NanoId right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(NanoId left, NanoId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(NanoId left, NanoId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Implicitly converts a <see cref="NanoId"/> to its string value.
    /// </summary>
    public static implicit operator string(NanoId id) => id.Value;

    /// <summary>
    /// Explicitly converts a string to a <see cref="NanoId"/> by parsing it.
    /// </summary>
    public static explicit operator NanoId(string s) => Parse(s);

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="NanoId"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<NanoId> OrdinalComparer => NanoIdOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="NanoId"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<NanoId> OrdinalIgnoreCaseComparer => NanoIdOrdinalIgnoreCaseComparer.Instance;

    private sealed class NanoIdOrdinalComparer : IEqualityComparer<NanoId>, IAlternateEqualityComparer<ReadOnlySpan<char>, NanoId> {
        public static NanoIdOrdinalComparer Instance { get; } = new();

        public bool Equals(NanoId x, NanoId y) => string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(NanoId obj) => obj.Value.GetHashCode(StringComparison.Ordinal);

        public bool Equals(ReadOnlySpan<char> alternate, NanoId other) => alternate.SequenceEqual(other.Value.AsSpan());

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.Ordinal);

        public NanoId Create(ReadOnlySpan<char> alternate) => NanoId.Parse(alternate);
    }

    private sealed class NanoIdOrdinalIgnoreCaseComparer : IEqualityComparer<NanoId>, IAlternateEqualityComparer<ReadOnlySpan<char>, NanoId> {
        public static NanoIdOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(NanoId x, NanoId y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(NanoId obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public bool Equals(ReadOnlySpan<char> alternate, NanoId other) => alternate.Equals(other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public NanoId Create(ReadOnlySpan<char> alternate) => NanoId.Parse(alternate);
    }

    #endregion
}

// -------------------------------------------------------------------------
// CONVERTERS
// -------------------------------------------------------------------------

/// <summary>
/// Provides precondition extension methods for <see cref="NanoId"/> validation.
/// </summary>
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
            if(length is <= 0 or > NanoId.MaxAllowedLength) {
                throw new ArgumentOutOfRangeException(nameof(length), $"Length must be between 1 and {NanoId.MaxAllowedLength}.");
            }
        }
    }
}

/// <summary>
/// Provides JSON serialization support for the <see cref="NanoId"/> struct.
/// </summary>
public sealed class NanoIdJsonConverter : JsonConverter<NanoId> {
    /// <inheritdoc/>
    public override NanoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            if(!reader.ValueIsEscaped && !reader.HasValueSequence) {
                if(NanoId.TryParse(reader.ValueSpan, out NanoId result)) {
                    return result;
                }
            }

            string? str = reader.GetString();
            if(str is not null && NanoId.TryParse(str, out NanoId parsed)) {
                return parsed;
            }

            throw new JsonException($"Unable to parse '{reader.GetString()}' as a valid NanoId.");
        }

        if(reader.TokenType == JsonTokenType.Null) {
            return NanoId.Empty;
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for NanoId.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, NanoId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override NanoId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? propName = reader.GetString();
        if(propName is not null && NanoId.TryParse(propName, out NanoId parsed)) {
            return parsed;
        }

        throw new JsonException($"Invalid property name format for NanoId: '{propName}'.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, NanoId value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}

/// <summary>
/// Provides type conversion support for the <see cref="NanoId"/> struct to and from string representations.
/// </summary>
public sealed class NanoIdTypeConverter : TypeConverter {
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if(value is string str)
            return NanoId.Parse(str);
        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        if(value is NanoId id && destinationType == typeof(string))
            return id.ToString();
        return base.ConvertTo(context, culture, value, destinationType);
    }
}