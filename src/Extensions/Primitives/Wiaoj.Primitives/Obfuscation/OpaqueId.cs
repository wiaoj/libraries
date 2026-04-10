using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
[TypeConverter(typeof(OpaqueIdTypeConverter))]
[JsonConverter(typeof(OpaqueIdJsonConverter))]
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("{ToString(),nq} [{Value}]")]
public readonly struct OpaqueId :
    IEquatable<OpaqueId>,
    ISpanParsable<OpaqueId>,
    ISpanFormattable,
    IUtf8SpanParsable<OpaqueId>,
    IUtf8SpanFormattable {
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
        return Parse(s.AsSpan());
    }

    /// <summary>Parses a <see cref="ReadOnlySpan{Char}"/> into an <see cref="OpaqueId"/>.</summary>
    public static OpaqueId Parse(ReadOnlySpan<char> s) {
        return TryParse(s, out OpaqueId r) ? r : throw new FormatException("Invalid OpaqueId format.");
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

    /// <summary>Parses a UTF-8 encoded byte span into an <see cref="OpaqueId"/>.</summary>
    public static OpaqueId Parse(ReadOnlySpan<byte> utf8Text) {
        return TryParse(utf8Text, out OpaqueId r) ? r : throw new FormatException("Invalid OpaqueId UTF-8 format.");
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

    /// <summary>
    /// Writes the 128-bit internal value to the destination span in little-endian format.
    /// </summary>
    /// <param name="destination">The destination span of bytes.</param>
    /// <returns><see langword="true"/> if the bytes were written; otherwise <see langword="false"/>.</returns>
    public bool TryWriteBytes(Span<byte> destination) {
        return MemoryMarshal.TryWrite(destination, in this._innerValue);
    }

    /// <summary>
    /// Creates an <see cref="OpaqueId"/> from a byte span (little-endian).
    /// </summary>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="result">The resulting <see cref="OpaqueId"/>.</param>
    /// <returns><see langword="true"/> if successful.</returns>
    public static bool TryReadBytes(ReadOnlySpan<byte> source, out OpaqueId result) {
        if(source.Length < 16) {
            result = default;
            return false;
        }
        result = new OpaqueId(MemoryMarshal.Read<Int128>(source));
        return true;
    }

    /// <summary>
    /// Writes the ID to an <see cref="System.Buffers.IBufferWriter{T}"/> as raw bytes.
    /// </summary>
    public void WriteTo(IBufferWriter<byte> writer) {
        Span<byte> span = writer.GetSpan(16);
        MemoryMarshal.TryWrite(span, in this._innerValue);
        writer.Advance(16);
    }

    #region Formatting

    /// <summary>Returns the obfuscated string representation of this <see cref="OpaqueId"/>.</summary>
    public override string ToString() {
        return ToString(null, null);
    }

    /// <summary>Returns the obfuscated string representation using the provided format.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        if(this._innerValue == 0) return "0";
        Span<char> buffer = stackalloc char[32];
        return TryFormat(buffer, out int written, default, default) ? buffer[..written].ToString() : string.Empty;
    }

    /// <summary>Tries to format this <see cref="OpaqueId"/> into the provided destination span.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this._innerValue == 0) {
            if(destination.Length < 1) { charsWritten = 0; return false; }
            destination[0] = '0'; charsWritten = 1; return true;
        }
        return Obfuscator.TryEncode(this._innerValue, destination, out charsWritten);
    }

    #endregion

    #region Operators & Equality

    /// <summary>Implicitly converts a <see cref="SnowflakeId"/> to an <see cref="OpaqueId"/>.</summary>
    public static implicit operator OpaqueId(SnowflakeId id) {
        return new(id);
    }

    /// <summary>Implicitly converts a <see cref="Guid"/> to an <see cref="OpaqueId"/>.</summary>
    public static implicit operator OpaqueId(Guid guid) {
        return new(guid);
    }

    /// <summary>Implicitly converts a <see cref="long"/> to an <see cref="OpaqueId"/>.</summary>
    public static implicit operator OpaqueId(long id) {
        return new(id);
    }

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to a <see cref="SnowflakeId"/>.</summary>
    public static explicit operator SnowflakeId(OpaqueId pid) {
        return pid.AsSnowflake();
    }

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to a <see cref="Guid"/>.</summary>
    public static explicit operator Guid(OpaqueId pid) {
        return pid.AsGuid();
    }

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to a <see cref="long"/>.</summary>
    public static explicit operator long(OpaqueId pid) {
        return (long)(ulong)pid._innerValue;
    }

    /// <summary>Explicitly converts an <see cref="OpaqueId"/> to an <see cref="Int128"/>.</summary>
    public static explicit operator Int128(OpaqueId pid) {
        return pid._innerValue;
    }

    /// <summary>
    /// Indicates whether the current <see cref="OpaqueId"/> is equal to another <see cref="OpaqueId"/>.
    /// </summary>
    /// <param name="other">The <see cref="OpaqueId"/> to compare with this instance.</param>
    /// <returns><see langword="true"/> if the current object is equal to the other parameter; otherwise, <see langword="false"/>.</returns>
    public bool Equals(OpaqueId other) {
        return this._innerValue == other._innerValue;
    }

    /// <inheritdoc cref="Equals(OpaqueId)"/>
    public override bool Equals(object? obj) {
        return obj is OpaqueId other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this._innerValue.GetHashCode();
    }

    /// <inheritdoc cref="Equals(OpaqueId)"/>
    public static bool operator ==(OpaqueId left, OpaqueId right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="Equals(OpaqueId)"/>
    public static bool operator !=(OpaqueId left, OpaqueId right) {
        return !left.Equals(right);
    }

    #endregion

    #region Interface Implementations

    static OpaqueId IParsable<OpaqueId>.Parse(string s, IFormatProvider? p) {
        return Parse(s);
    }

    static bool IParsable<OpaqueId>.TryParse(string? s, IFormatProvider? p, out OpaqueId r) {
        return TryParse(s.AsSpan(), out r);
    }

    static OpaqueId ISpanParsable<OpaqueId>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) {
        return Parse(s);
    }

    static bool ISpanParsable<OpaqueId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out OpaqueId r) {
        return TryParse(s, out r);
    }

    static OpaqueId IUtf8SpanParsable<OpaqueId>.Parse(ReadOnlySpan<byte> b, IFormatProvider? p) {
        return Parse(b);
    }

    static bool IUtf8SpanParsable<OpaqueId>.TryParse(ReadOnlySpan<byte> b, IFormatProvider? p, out OpaqueId r) {
        return TryParse(b, out r);
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p) {
        Span<char> charBuffer = stackalloc char[32];
        if(!TryFormat(charBuffer, out int charsWritten, format, p)) {
            written = 0; return false;
        }
        return Encoding.UTF8.TryGetBytes(charBuffer[..charsWritten], dest, out written);
    }

    #endregion
}