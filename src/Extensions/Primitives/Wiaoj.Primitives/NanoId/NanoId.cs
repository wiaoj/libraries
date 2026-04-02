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
[DebuggerDisplay("{ToString(),nq}")]
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

    /// <summary>
    /// The default length for a generated <see cref="NanoId"/>.
    /// </summary>
    private const int DefaultLength = 21;

    /// <summary>
    /// The maximum permitted length for a <see cref="NanoId"/> to prevent excessive memory allocation or denial-of-service attacks.
    /// </summary>
    internal const int MaxAllowedLength = 128;

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
        throw new FormatException($"Invalid NanoId format. Contains illegal characters or invalid length.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="NanoId"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result if successful, or <see cref="Empty"/> if failed.</param>
    /// <returns><see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out NanoId result) {
        if(s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="NanoId"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="result">When this method returns, contains the parsed result if successful, or <see cref="Empty"/> if failed.</param>
    /// <returns><see langword="true"/> if the span was successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out NanoId result) {
        return TryParseInternal(s, out result);
    }

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
    public override string ToString() {
        return this.Value;
    }

    /// <summary>
    /// Attempts to format the <see cref="NanoId"/> into the provided character span.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="charsWritten">The number of characters written to the destination.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
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

    // -------------------------------------------------------------------------
    // EXPLICIT INTERFACES
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    static NanoId IParsable<NanoId>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    /// <inheritdoc/>
    static bool IParsable<NanoId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out NanoId result) {
        return TryParse(s, out result);
    }

    /// <inheritdoc/>
    static NanoId ISpanParsable<NanoId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    /// <inheritdoc/>
    static bool ISpanParsable<NanoId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out NanoId result) {
        return TryParse(s, out result);
    }

    /// <inheritdoc/>
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    /// <inheritdoc/>
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    // -------------------------------------------------------------------------
    // EQUALITY & OPERATORS
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool Equals(NanoId other) {
        return string.Equals(this._value, other._value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this._value?.GetHashCode() ?? 0;
    }

    /// <inheritdoc/>
    public int CompareTo(NanoId other) {
        return string.CompareOrdinal(this._value, other._value);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        return obj is NanoId other ? CompareTo(other) : 1;
    }

    /// <summary>
    /// Implicitly converts a <see cref="NanoId"/> to its string value.
    /// </summary>
    public static implicit operator string(NanoId id) {
        return id.Value;
    }

    /// <summary>
    /// Explicitly converts a string to a <see cref="NanoId"/> by parsing it.
    /// </summary>
    public static explicit operator NanoId(string s) {
        return Parse(s);
    }
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
            if(reader.HasValueSequence)
                return NanoId.Parse(reader.GetString()!);
            return NanoId.Parse(reader.GetString()!);
        }
        return NanoId.Empty;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, NanoId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override NanoId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return NanoId.Parse(reader.GetString()!);
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