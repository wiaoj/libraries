using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;
using Wiaoj.Primitives.Snowflake;
using Wiaoj.Primitives.Obfuscation;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents an opaque (obfuscated), URL-friendly identifier wrapper for 64-bit (Snowflake) or 128-bit (Guid) IDs.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("{ToString(),nq} [{Value}]")]
[TypeConverter(typeof(OpaqueIdTypeConverter))]
[JsonConverter(typeof(OpaqueIdJsonConverter))]
public readonly struct OpaqueId :
    IEquatable<OpaqueId>,
    IComparable<OpaqueId>,
    IComparable,
    IParsable<OpaqueId>,
    ISpanParsable<OpaqueId>,
    IUtf8SpanParsable<OpaqueId>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<OpaqueId, OpaqueId, bool> {
    private static readonly Lock _configLock = new();

    private static IObfuscator Obfuscator {
        get {
            Preca.ThrowIfNull(
                field,
                () => new InvalidOperationException("OpaqueId is not configured. Call OpaqueId.Configure() at application startup."));
            return field;
        }
        set;
    }

    /// <summary>
    /// Configures the global obfuscation strategy for all OpaqueId instances.
    /// </summary>
    /// <param name="customObfuscator">The obfuscation strategy to be used globally.</param>
    public static void Configure(IObfuscator customObfuscator) {
        Preca.ThrowIfNull(customObfuscator);
        lock(_configLock) {
            Obfuscator = customObfuscator;
        }
    }

    private readonly Int128 _innerValue;

    /// <summary>
    /// Gets the raw internal 128-bit identifier value.
    /// </summary>
    public Int128 Value => this._innerValue;

    /// <summary>
    /// Returns an empty <see cref="OpaqueId"/> representing a zero-value identifier.
    /// </summary>
    public static OpaqueId Empty { get; } = default;

    /// <summary>
    /// Returns <see langword="true"/> if the value fits within 64 bits (standard Snowflake range).
    /// </summary>
    public bool Is64Bit => (this._innerValue >> 64) == 0;

    #region Constructors

    /// <summary>Initializes a new <see cref="OpaqueId"/> from a <see cref="SnowflakeId"/>.</summary>
    public OpaqueId(SnowflakeId id) {
        this._innerValue = (Int128)(ulong)id.Value;
    }

    /// <summary>Initializes a new <see cref="OpaqueId"/> from a <see cref="Guid"/>.</summary>
    public OpaqueId(Guid guid) {
        this._innerValue = Unsafe.BitCast<Guid, Int128>(guid);
    }

    /// <summary>Initializes a new <see cref="OpaqueId"/> from a 64-bit integer.</summary>
    public OpaqueId(long raw) {
        this._innerValue = (Int128)(ulong)raw;
    }

    /// <summary>Initializes a new <see cref="OpaqueId"/> from a 128-bit integer.</summary>
    public OpaqueId(Int128 raw) {
        this._innerValue = raw;
    }

    #endregion

    #region Conversion Methods

    /// <summary>Converts the internal value to a <see cref="SnowflakeId"/>.</summary>
    public SnowflakeId AsSnowflake() {
        return new SnowflakeId((long)(ulong)this._innerValue);
    }

    /// <summary>Converts the internal value to a <see cref="Guid"/>.</summary>
    public Guid AsGuid() {
        return Unsafe.BitCast<Int128, Guid>(this._innerValue);
    }

    #endregion

    #region Parsing

    /// <summary>Parses a <see cref="string"/> into an <see cref="OpaqueId"/>.</summary>
    public static OpaqueId Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>Parses a <see cref="ReadOnlySpan{Char}"/> into an <see cref="OpaqueId"/>.</summary>
    public static OpaqueId Parse(ReadOnlySpan<char> s) {
        return TryParse(s, out OpaqueId r) ? r : throw new FormatException("Invalid OpaqueId format.");
    }

    /// <summary>Parses a UTF-8 encoded byte span into an <see cref="OpaqueId"/>.</summary>
    public static OpaqueId Parse(ReadOnlySpan<byte> utf8Text) {
        return TryParse(utf8Text, out OpaqueId r) ? r : throw new FormatException("Invalid OpaqueId UTF-8 format.");
    }

    /// <summary>Tries to parse a string into an <see cref="OpaqueId"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out OpaqueId result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a <see cref="ReadOnlySpan{Char}"/> into an <see cref="OpaqueId"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out OpaqueId result) {
        if(s.IsEmpty) { result = default; return false; }
        if(s.Length == 1 && s[0] == '0') { result = Empty; return true; }

        if(Obfuscator.TryDecode(s, out Int128 rawId)) {
            result = new OpaqueId(rawId);
            return true;
        }
        result = default; return false;
    }

    /// <summary>Tries to parse a UTF-8 encoded byte span into an <see cref="OpaqueId"/>.</summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out OpaqueId result) {
        if(utf8Text.IsEmpty) { result = default; return false; }
        if(utf8Text.Length == 1 && utf8Text[0] == (byte)'0') { result = Empty; return true; }

        if(Obfuscator.TryDecodeUtf8(utf8Text, out Int128 rawId)) {
            result = new OpaqueId(rawId);
            return true;
        }
        result = default; return false;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static OpaqueId IParsable<OpaqueId>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<OpaqueId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out OpaqueId result) => TryParse(s, out result);
    static OpaqueId ISpanParsable<OpaqueId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<OpaqueId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out OpaqueId result) => TryParse(s, out result);
    static OpaqueId IUtf8SpanParsable<OpaqueId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<OpaqueId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out OpaqueId result) => TryParse(utf8Text, out result);

    #endregion

    #region Formatting

    /// <summary>Returns the obfuscated string representation of this <see cref="OpaqueId"/>.</summary>
    public override string ToString() => ToString(null, null);

    /// <summary>Returns the obfuscated string representation using the provided format.</summary>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>Returns the obfuscated string representation using the provided format and provider.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        if(this._innerValue == 0) return "0";
        Span<char> buffer = stackalloc char[32];
        return TryFormat(buffer, out int written, default, default) ? buffer[..written].ToString() : string.Empty;
    }

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination span.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination span using the specified format.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination span using the specified format and provider.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this._innerValue == 0) {
            if(destination.Length < 1) { charsWritten = 0; return false; }
            destination[0] = '0'; charsWritten = 1; return true;
        }
        return Obfuscator.TryEncode(this._innerValue, destination, out charsWritten);
    }

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination UTF-8 byte span.</summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination UTF-8 byte span using the specified format.</summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination UTF-8 byte span using the specified format and provider.</summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        Span<char> charBuffer = stackalloc char[32];
        if(!TryFormat(charBuffer, out int charsWritten, format, provider)) {
            bytesWritten = 0; return false;
        }
        return Encoding.UTF8.TryGetBytes(charBuffer[..charsWritten], utf8Destination, out bytesWritten);
    }

    #endregion

    #region Operators, Comparison & Equality

    /// <summary>Implicitly converts a <see cref="SnowflakeId"/> to an <see cref="OpaqueId"/>.</summary>
    public static implicit operator OpaqueId(SnowflakeId id) => new(id);

    /// <summary>Implicitly converts a <see cref="Guid"/> to an <see cref="OpaqueId"/>.</summary>
    public static implicit operator OpaqueId(Guid guid) => new(guid);

    /// <summary>Implicitly converts a <see cref="long"/> to an <see cref="OpaqueId"/>.</summary>
    public static implicit operator OpaqueId(long id) => new(id);

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to a <see cref="SnowflakeId"/>.</summary>
    public static explicit operator SnowflakeId(OpaqueId pid) => pid.AsSnowflake();

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to a <see cref="Guid"/>.</summary>
    public static explicit operator Guid(OpaqueId pid) => pid.AsGuid();

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to a <see cref="long"/>.</summary>
    public static explicit operator long(OpaqueId pid) => (long)(ulong)pid._innerValue;

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to an <see cref="Int128"/>.</summary>
    public static explicit operator Int128(OpaqueId pid) => pid._innerValue;

    /// <summary>
    /// Indicates whether the current <see cref="OpaqueId"/> is equal to another <see cref="OpaqueId"/>.
    /// </summary>
    public bool Equals(OpaqueId other) => this._innerValue == other._innerValue;

    /// <inheritdoc cref="Equals(OpaqueId)"/>
    public override bool Equals(object? obj) => obj is OpaqueId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => this._innerValue.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(OpaqueId other) => this._innerValue.CompareTo(other._innerValue);

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is OpaqueId other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(OpaqueId)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(OpaqueId left, OpaqueId right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(OpaqueId left, OpaqueId right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(OpaqueId left, OpaqueId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(OpaqueId left, OpaqueId right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="Equals(OpaqueId)"/>
    public static bool operator ==(OpaqueId left, OpaqueId right) => left.Equals(right);

    /// <inheritdoc cref="Equals(OpaqueId)"/>
    public static bool operator !=(OpaqueId left, OpaqueId right) => !left.Equals(right);

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs equality comparisons on <see cref="OpaqueId"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<OpaqueId> OrdinalComparer => OpaqueIdOrdinalComparer.Instance;

    private sealed class OpaqueIdOrdinalComparer : IEqualityComparer<OpaqueId>, IAlternateEqualityComparer<ReadOnlySpan<char>, OpaqueId> {
        public static OpaqueIdOrdinalComparer Instance { get; } = new();

        public bool Equals(OpaqueId x, OpaqueId y) => x._innerValue == y._innerValue;

        public int GetHashCode(OpaqueId obj) => obj._innerValue.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, OpaqueId other) {
            if(OpaqueId.TryParse(alternate, out OpaqueId parsed)) {
                return parsed._innerValue == other._innerValue;
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(OpaqueId.TryParse(alternate, out OpaqueId parsed)) {
                return parsed._innerValue.GetHashCode();
            }
            return 0;
        }

        public OpaqueId Create(ReadOnlySpan<char> alternate) => OpaqueId.Parse(alternate);
    }

    #endregion
}