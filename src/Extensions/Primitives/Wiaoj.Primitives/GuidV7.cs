using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a strongly-typed UUID version 7 identifier.
/// </summary>
/// <remarks>
/// UUID v7 is time-ordered (k-sorted), making it significantly more efficient than random UUIDs (v4)
/// for database primary keys — avoids page splits and index fragmentation in B-tree indexes.
/// <para>
/// This wrapper enforces that only v7 GUIDs are used where a time-ordered identity is expected,
/// eliminating the ambiguity of a plain <see cref="Guid"/> (v4? v7? random?).
/// </para>
/// Internally uses <see cref="Guid.CreateVersion7()"/> introduced in .NET 9.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(GuidV7JsonConverter))]
public readonly record struct GuidV7 :
    IEquatable<GuidV7>,
    IComparable<GuidV7>,
    ISpanParsable<GuidV7>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly Guid _value;

    /// <summary>Gets the underlying <see cref="Guid"/> value.</summary>
    public Guid Value => this._value;

    /// <summary>
    /// Represents an empty (all-zeros) GuidV7.
    /// Not a valid time-ordered ID — use <see cref="Create()"/> for generation.
    /// </summary>
    public static GuidV7 Empty => new(Guid.Empty);

    private GuidV7(Guid value) {
        this._value = value;
    }

    #region Generation

    /// <summary>
    /// Generates a new time-ordered UUID v7.
    /// Each call produces a monotonically increasing value within the same millisecond.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GuidV7 Create() {
        return new(Guid.CreateVersion7());
    }

    /// <summary>
    /// Generates a new UUID v7 seeded from the provided <see cref="TimeProvider"/>.
    /// Useful for deterministic testing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GuidV7 Create(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        return new(Guid.CreateVersion7(timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Generates a new UUID v7 seeded from the provided <see cref="Wiaoj.Primitives.UnixTimestamp"/>.
    /// </summary>
    /// <remarks>
    /// Useful when the creation time is already known (e.g., reconstructing an ID from
    /// an existing record, or generating IDs with a specific timestamp in tests).
    /// The embedded timestamp will reflect the given <paramref name="timestamp"/> value
    /// with millisecond precision.
    /// </remarks>
    /// <param name="timestamp">The Unix timestamp to embed into the UUID v7.</param>
    /// <returns>A new <see cref="GuidV7"/> with the given timestamp encoded in its high bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GuidV7 Create(UnixTimestamp timestamp) {
        return new(Guid.CreateVersion7(timestamp.ToDateTimeOffset()));
    }

    #endregion

    #region Timestamp Extraction

    /// <summary>
    /// Extracts the timestamp embedded in the UUID v7 (millisecond precision).
    /// </summary>
    /// <remarks>
    /// UUID v7 layout (RFC 9562): bits 0-47 = unix_ts_ms (48-bit big-endian).
    /// </remarks>
    public DateTimeOffset GetTimestamp() {
        Span<byte> bytes = stackalloc byte[16];
        this._value.TryWriteBytes(bytes, true, out _);

        long ms = ((long)bytes[0] << 40)
                | ((long)bytes[1] << 32)
                | ((long)bytes[2] << 24)
                | ((long)bytes[3] << 16)
                | ((long)bytes[4] << 8)
                | bytes[5];

        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }

    /// <summary>Gets the <see cref="UnixTimestamp"/> embedded in this UUID v7.</summary>
    public UnixTimestamp UnixTimestamp =>
        UnixTimestamp.FromMilliseconds(GetTimestamp().ToUnixTimeMilliseconds());

    #endregion

    #region Conversion

    /// <summary>Returns the underlying <see cref="Guid"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Guid ToGuid() {
        return this._value;
    }

    /// <summary>
    /// Encodes the GUID bytes as a <see cref="Base64UrlString"/> (22-char, no padding).
    /// Compact URL-safe representation.
    /// </summary>
    public Base64UrlString ToBase64Url() {
        Span<byte> bytes = stackalloc byte[16];
        this._value.TryWriteBytes(bytes, bigEndian: true, out _);
        return Base64UrlString.FromBytes(bytes);
    }

    /// <summary>
    /// Encodes the GUID bytes as a <see cref="HexString"/> (32 hex chars, no dashes).
    /// </summary>
    public HexString ToHexString() {
        Span<byte> bytes = stackalloc byte[16];
        this._value.TryWriteBytes(bytes, bigEndian: true, out _);
        return HexString.FromBytes(bytes);
    }

    /// <summary>
    /// Tries to write the 16 bytes of the UUID v7 to the specified span.
    /// </summary>
    /// <param name="destination">The span to which the 16 bytes should be written.</param>
    /// <param name="bigEndian">
    /// <see langword="true"/> to write the bytes in big-endian order (RFC 9562 standard); 
    /// <see langword="false"/> for little-endian (legacy Windows GUID format).
    /// </param>
    /// <param name="bytesWritten">The number of bytes written to the <paramref name="destination"/> (always 16 on success).</param>
    /// <returns><see langword="true"/> if the span is large enough (at least 16 bytes); otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBytes(Span<byte> destination, bool bigEndian, out int bytesWritten) {
        return this._value.TryWriteBytes(destination, bigEndian, out bytesWritten);
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="GuidV7"/>.
    /// Accepts any standard GUID format (D, N, B, P, X).
    /// </summary>
    /// <exception cref="FormatException">The string is not a valid version-7 GUID.</exception>
    public static GuidV7 Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>Parses a character span into a <see cref="GuidV7"/>.</summary>
    /// <exception cref="FormatException">The span is not a valid version-7 GUID.</exception>
    public static GuidV7 Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out GuidV7 result)) return result;
        throw new FormatException($"'{s}' is not a valid GuidV7. Ensure the GUID is version 7.");
    }

    /// <summary>Tries to parse a string into a <see cref="GuidV7"/>.</summary>
    /// <returns><see langword="false"/> if the string is not a GUID or not version 7.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out GuidV7 result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a character span into a <see cref="GuidV7"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out GuidV7 result) {
        if(!Guid.TryParse(s, out Guid guid) || guid.Version != 7) {
            result = default;
            return false;
        }
        result = new GuidV7(guid);
        return true;
    }

    // Explicit interface implementations (IFormatProvider hidden from public API)
    static GuidV7 IParsable<GuidV7>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<GuidV7>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out GuidV7 result) {
        return TryParse(s, out result);
    }

    static GuidV7 ISpanParsable<GuidV7>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<GuidV7>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out GuidV7 result) {
        return TryParse(s, out result);
    }

    #endregion

    #region Formatting

    /// <summary>Returns the standard hyphenated GUID string (format "D").</summary>
    /// <example>01968e3a-b4c2-7f00-a1b2-c3d4e5f60718</example>
    public override string ToString() {
        return this._value.ToString("D");
    }

    /// <summary>
    /// Returns the GUID string using the specified format specifier.
    /// Supported: D (default, hyphenated), N (no dashes), B (braces), P (parens), X (hex struct).
    /// </summary>
    public string ToString(string? format) {
        return this._value.ToString(format ?? "D");
    }

    /// <summary>
    /// Tries to format the value of the current <see cref="GuidV7"/> instance into the provided span of characters.
    /// </summary>
    /// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination"/>.</param>
    /// <param name="format">A read-only span that contains the character format string (e.g., "D", "N", "B", "P", "X").</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default) {
        return this._value.TryFormat(destination, out charsWritten, format);
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return ToString(format);
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        string fmt = format.IsEmpty ? "D" : format.ToString();
        return this._value.TryFormat(destination, out charsWritten, fmt);
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        // "D" = 36 chars, "N" = 32, "B"/"P" = 38, "X" = ~68. 68 covers all.
        Span<char> charBuf = stackalloc char[68];
        string fmt = format.IsEmpty ? "D" : format.ToString();
        if(!this._value.TryFormat(charBuf, out int charsWritten, fmt)) {
            bytesWritten = 0;
            return false;
        }
        if(utf8Destination.Length < charsWritten) { bytesWritten = 0; return false; }
        bytesWritten = System.Text.Encoding.UTF8.GetBytes(charBuf[..charsWritten], utf8Destination);
        return true;
    }

    #endregion

    #region Comparison & Operators

    /// <inheritdoc/>
    public int CompareTo(GuidV7 other) {
        return this._value.CompareTo(other._value);
    }

    /// <inheritdoc/>
    public bool Equals(GuidV7 other) {
        return this._value == other._value;
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this._value.GetHashCode();
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(GuidV7 left, GuidV7 right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(GuidV7 left, GuidV7 right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(GuidV7 left, GuidV7 right) {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(GuidV7 left, GuidV7 right) {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Implicitly converts a <see cref="GuidV7"/> to <see cref="Guid"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Guid(GuidV7 id) {
        return id._value;
    }

    /// <summary>
    /// Explicitly converts a <see cref="Guid"/> to <see cref="GuidV7"/>.
    /// </summary>
    /// <exception cref="InvalidCastException">Thrown if the GUID is not version 7.</exception>
    public static explicit operator GuidV7(Guid g) {
        if(g.Version != 7)
            throw new InvalidCastException($"Cannot convert Guid v{g.Version} to GuidV7. Only version 7 is supported.");
        return new GuidV7(g);
    }

    #endregion
}

/// <summary>JSON converter for <see cref="GuidV7"/>. Serializes as hyphenated GUID string.</summary>
public sealed class GuidV7JsonConverter : JsonConverter<GuidV7> {
    /// <inheritdoc/>
    public override GuidV7 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string token for GuidV7.");
        string? s = reader.GetString();
        if(GuidV7.TryParse(s, out GuidV7 result))
            return result;
        throw new JsonException($"'{s}' is not a valid GuidV7.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, GuidV7 value, JsonSerializerOptions options) {
        Span<char> buffer = stackalloc char[36]; // "D" format = 36 chars
        value.TryFormat(buffer, out _, "D");
        writer.WriteStringValue(buffer);
    }
}