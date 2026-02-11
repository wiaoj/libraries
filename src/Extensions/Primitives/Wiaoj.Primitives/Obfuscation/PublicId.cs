using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives.Snowflake;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents an obfuscated, URL-friendly identifier wrapper for 64-bit (Snowflake) or 128-bit (Guid) IDs.
/// <para>
/// This type ensures IDOR safety by scrambling sequential IDs while providing a stable "0" representation 
/// for empty or default states.
/// </para>
/// </summary>
[TypeConverter(typeof(PublicIdTypeConverter))]
[JsonConverter(typeof(PublicIdJsonConverter))]
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("{ToString(),nq} [{Value}]")]
public readonly struct PublicId :
    IEquatable<PublicId>,
    ISpanParsable<PublicId>,
    ISpanFormattable,
    IUtf8SpanParsable<PublicId> {

    // --- Global Configuration ---
    private static uint[] _keys = IdCipher.DeriveKeys("DefaultInsecureSeed");
    private static bool _isConfigured = false;
    private static readonly Lock _configLock = new();

    /// <summary>
    /// Configures the global obfuscation logic with a specific seed.
    /// This should be called once at application startup.
    /// </summary>
    /// <param name="seed">The secret key used for scrambling bit patterns.</param>
    public static void Configure(string seed) {
        Preca.ThrowIfNullOrWhiteSpace(seed);
        lock(_configLock) {
            _keys = IdCipher.DeriveKeys(seed);
            _isConfigured = true;
        }
    }

    // --- State ---
    private readonly Int128 _innerValue;

    /// <summary>
    /// Returns the underlying raw value as a <see cref="SnowflakeId"/>.
    /// </summary>
    /// <remarks>Possible data loss if the original source was a 128-bit Guid.</remarks>
    public SnowflakeId AsSnowflake() => new SnowflakeId((long)(ulong)_innerValue);

    /// <summary>
    /// Returns the underlying raw value as a <see cref="Guid"/>.
    /// </summary>
    public Guid AsGuid() => Unsafe.BitCast<Int128, Guid>(_innerValue);

    /// <summary>
    /// Gets the raw 128-bit internal value of the identifier.
    /// </summary>
    public Int128 Value => _innerValue;

    /// <summary>
    /// Represents an empty or default <see cref="PublicId"/> (Value = 0).
    /// </summary>
    public static PublicId Empty { get; } = default;

    /// <summary>
    /// Gets a value indicating whether this identifier fits within 64 bits (standard Snowflake range).
    /// </summary>
    public bool Is64Bit => (_innerValue >> 64) == 0;

    // --- Constructors ---

    /// <summary>Initializes a new instance from a <see cref="SnowflakeId"/>.</summary>
    public PublicId(SnowflakeId id) => _innerValue = (Int128)(ulong)id.Value;

    /// <summary>Initializes a new instance from a <see cref="Guid"/>.</summary>
    public PublicId(Guid guid) => _innerValue = Unsafe.BitCast<Guid, Int128>(guid);

    /// <summary>Initializes a new instance from a raw 64-bit long integer.</summary>
    public PublicId(long raw) => _innerValue = (Int128)(ulong)raw;

    /// <summary>Initializes a new instance from a raw 128-bit integer.</summary>
    public PublicId(Int128 raw) => _innerValue = raw;

    // --- Parsing (Char Span) ---

    /// <inheritdoc cref="Parse(ReadOnlySpan{char})"/>
    public static PublicId Parse(string s) => Parse(s.AsSpan());

    /// <summary>
    /// Parses a character span into a <see cref="PublicId"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <returns>The parsed PublicId.</returns>
    /// <exception cref="FormatException">Thrown if the format is invalid or characters are illegal.</exception>
    public static PublicId Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, null, out PublicId result)) return result;
        throw new FormatException($"Invalid PublicId format: '{s.ToString()}'");
    }

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, IFormatProvider?, out PublicId)"/>
    public static bool TryParse(ReadOnlySpan<char> s, out PublicId result)
        => TryParse(s, null, out result);

    /// <summary>
    /// Tries to parse a character span into a <see cref="PublicId"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="provider">Format provider (ignored).</param>
    /// <param name="result">The resulting PublicId if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out PublicId result) {
        if(s.IsEmpty) { result = default; return false; }

        // [SPECIAL CASE] Handle Zero/Empty semantically
        if(s.Length == 1 && s[0] == '0') {
            result = Empty;
            return true;
        }

        if(!TryDecodeBase62(s, out Int128 scrambled)) {
            result = default;
            return false;
        }

        Int128 descrambled = (scrambled >> 64) == 0
            ? (Int128)IdCipher.Decrypt64((ulong)scrambled, _keys)
            : IdCipher.Decrypt128(scrambled, _keys);

        result = new PublicId(descrambled);
        return true;
    }

    // --- Parsing (UTF-8 Byte Span) ---

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="PublicId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded bytes to parse.</param>
    /// <returns>The parsed PublicId.</returns>
    public static PublicId Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, null, out var result)) return result;
        throw new FormatException("Invalid PublicId UTF-8 format.");
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="PublicId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded bytes to parse.</param>
    /// <param name="provider">Format provider (ignored).</param>
    /// <param name="result">The resulting PublicId if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out PublicId result) {
        if(utf8Text.IsEmpty) { result = default; return false; }

        // [SPECIAL CASE] Handle Zero/Empty semantically
        if(utf8Text.Length == 1 && utf8Text[0] == (byte)'0') {
            result = Empty;
            return true;
        }

        if(!TryDecodeBase62Utf8(utf8Text, out Int128 scrambled)) {
            result = default;
            return false;
        }

        Int128 descrambled = (scrambled >> 64) == 0
            ? (Int128)IdCipher.Decrypt64((ulong)scrambled, _keys)
            : IdCipher.Decrypt128(scrambled, _keys);

        result = new PublicId(descrambled);
        return true;
    }

    // --- Formatting ---

    /// <summary>
    /// Returns the obfuscated string representation of the identifier.
    /// Returns "0" if the identifier is <see cref="Empty"/>.
    /// </summary>
    public override string ToString() => ToString(null, null);

    /// <inheritdoc cref="IFormattable.ToString(string?, IFormatProvider?)"/>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        if(_innerValue == 0) return "0";

        Span<char> buffer = stackalloc char[32];
        if(TryFormat(buffer, out int written, default, default)) {
            return buffer[..written].ToString();
        }
        return string.Empty;
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        // [SPECIAL CASE] 0 handles as "0"
        if(_innerValue == 0) {
            if(destination.Length < 1) { charsWritten = 0; return false; }
            destination[0] = '0';
            charsWritten = 1;
            return true;
        }

        Int128 scrambled = Is64Bit
            ? (Int128)IdCipher.Encrypt64((ulong)_innerValue, _keys)
            : IdCipher.Encrypt128(_innerValue, _keys);

        return TryEncodeBase62(scrambled, destination, out charsWritten);
    }

    // --- Base62 Core Logic ---

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static bool TryEncodeBase62(Int128 value, Span<char> destination, out int charsWritten) {
        UInt128 v = (UInt128)value;
        if(v == 0) {
            if(destination.Length < 1) { charsWritten = 0; return false; }
            destination[0] = '0';
            charsWritten = 1;
            return true;
        }

        int i = 0;
        Span<char> buffer = stackalloc char[32];
        while(v > 0) {
            (v, UInt128 rem) = UInt128.DivRem(v, 62);
            buffer[i++] = Alphabet[(int)rem];
        }

        if(destination.Length < i) { charsWritten = 0; return false; }
        for(int j = 0; j < i; j++) destination[j] = buffer[i - 1 - j];
        charsWritten = i;
        return true;
    }

    private static bool TryDecodeBase62(ReadOnlySpan<char> s, out Int128 result) {
        UInt128 v = 0;
        result = 0;

        // 128-bit bir sayı Base62 formatında en fazla 22 karakter olabilir.
        // Eğer daha uzunsa direkt geçersizdir.
        if(s.IsEmpty || s.Length > 22) return false;

        foreach(char c in s) {
            int val = c switch {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'Z' => c - 'A' + 10,
                >= 'a' and <= 'z' => c - 'a' + 36,
                _ => -1
            };

            if(val == -1) return false;

            // Overflow kontrolü: .NET checked bloğu UInt128 için de çalışır.
            try {
                checked {
                    v = (v * 62) + (uint)val;
                }
            }
            catch(OverflowException) {
                return false;
            }
        }
        result = (Int128)v;
        return true;
    }

    private static bool TryDecodeBase62Utf8(ReadOnlySpan<byte> utf8Text, out Int128 result) {
        UInt128 v = 0;
        result = 0;

        // UTF-8 için de aynı uzunluk sınırı geçerli
        if(utf8Text.IsEmpty || utf8Text.Length > 22) return false;

        foreach(byte b in utf8Text) {
            int val = b switch {
                >= (byte)'0' and <= (byte)'9' => b - '0',
                >= (byte)'A' and <= (byte)'Z' => b - 'A' + 10,
                >= (byte)'a' and <= (byte)'z' => b - 'a' + 36,
                _ => -1
            };

            if(val == -1) return false;

            try {
                checked {
                    v = (v * 62) + (uint)val;
                }
            }
            catch(OverflowException) {
                return false;
            }
        }
        result = (Int128)v;
        return true;
    }

    // --- Explicit Interface Implementations ---

    static PublicId IParsable<PublicId>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<PublicId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PublicId result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), provider, out result);
    }
    static PublicId ISpanParsable<PublicId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<PublicId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out PublicId result) => TryParse(s, provider, out result);

    static PublicId IUtf8SpanParsable<PublicId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<PublicId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out PublicId result) => TryParse(utf8Text, provider, out result);

    // --- Operators & Equality ---
    /// <summary>Implicitly converts a <see cref="SnowflakeId"/> to a <see cref="PublicId"/>.</summary>
    public static implicit operator PublicId(SnowflakeId id) => new(id);
    /// <summary>Implicitly converts a <see cref="Guid"/> to a <see cref="PublicId"/>.</summary>
    public static implicit operator PublicId(Guid guid) => new(guid);
    /// <summary>Implicitly converts a raw long ID to a <see cref="PublicId"/>.</summary>
    public static implicit operator PublicId(long id) => new(id);
    /// <summary>Explicitly converts a <see cref="PublicId"/> to a <see cref="SnowflakeId"/>.</summary>
    public static explicit operator SnowflakeId(PublicId pid) => pid.AsSnowflake();
    /// <summary>Explicitly converts a <see cref="PublicId"/> to a <see cref="Guid"/>.</summary>
    public static explicit operator Guid(PublicId pid) => pid.AsGuid();

    /// <summary>Explicitly converts to a 64-bit signed integer.</summary>
    public static explicit operator long(PublicId pid) => (long)(ulong)pid._innerValue;

    /// <summary>Explicitly converts to a 64-bit unsigned integer.</summary>
    public static explicit operator ulong(PublicId pid) => (ulong)pid._innerValue;

    /// <summary>Explicitly converts to a 128-bit integer.</summary>
    public static explicit operator Int128(PublicId pid) => pid._innerValue;

    /// <inheritdoc/>
    public bool Equals(PublicId other) => _innerValue == other._innerValue;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PublicId other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => _innerValue.GetHashCode();
    /// <summary>Checks if two <see cref="PublicId"/> instances are equal.</summary>
    public static bool operator ==(PublicId left, PublicId right) => left.Equals(right);
    /// <summary>Checks if two <see cref="PublicId"/> instances are not equal.</summary>
    public static bool operator !=(PublicId left, PublicId right) => !left.Equals(right);
}