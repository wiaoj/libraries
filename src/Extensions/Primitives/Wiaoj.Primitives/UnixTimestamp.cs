using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a Unix Timestamp in MILLISECONDS (milliseconds elapsed since 1970-01-01 00:00:00 UTC).
/// This value object wraps a <see cref="long"/> to ensure type safety, eliminate "primitive obsession",
/// and provide high-performance parsing/formatting capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="ISpanParsable{TSelf}"/> and <see cref="IUtf8SpanParsable{TSelf}"/> for 
/// zero-allocation parsing from web requests and JSON.
/// </para>
/// <para>
/// Stores precision up to milliseconds.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString()} ({ToDateTimeUtc(),nq})")]
[JsonConverter(typeof(UnixTimestampJsonConverter))]
public readonly record struct UnixTimestamp :
    IComparable<UnixTimestamp>,
    ISpanParsable<UnixTimestamp>,
    ISpanFormattable,
    IUtf8SpanParsable<UnixTimestamp>,
    IUtf8SpanFormattable,
    IAdditionOperators<UnixTimestamp, TimeSpan, UnixTimestamp>,
    ISubtractionOperators<UnixTimestamp, TimeSpan, UnixTimestamp>,
    ISubtractionOperators<UnixTimestamp, UnixTimestamp, TimeSpan>,
    IEqualityOperators<UnixTimestamp, long, bool>,
    IComparisonOperators<UnixTimestamp, UnixTimestamp, bool> {

    // -------------------------------------------------------------------------
    // CONSTANTS & FIELDS
    // -------------------------------------------------------------------------

    private readonly long _milliseconds;

    /// <summary>
    /// Represents the Unix Epoch (1970-01-01T00:00:00Z). Value is 0.
    /// </summary>
    public static UnixTimestamp Epoch { get; } = new(0);

    /// <summary>
    /// Represents the minimum representable timestamp (MinValue of Int64).
    /// Roughly 292 million years in the past.
    /// </summary>
    public static UnixTimestamp MinValue { get; } = new(long.MinValue);

    /// <summary>
    /// Represents the maximum representable timestamp (MaxValue of Int64).
    /// Roughly 292 million years in the future.
    /// </summary>
    public static UnixTimestamp MaxValue { get; } = new(long.MaxValue);

    /// <summary>
    /// Gets the raw number of milliseconds since the Unix Epoch.
    /// </summary>
    public long Milliseconds => _milliseconds;

    // Private constructor to enforce creation via static factory methods.
    private UnixTimestamp(long milliseconds) {
        _milliseconds = milliseconds;
    }

    // -------------------------------------------------------------------------
    // FACTORIES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the current Unix timestamp based on <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public static UnixTimestamp Now
        => From(DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from raw milliseconds.
    /// </summary>
    /// <param name="milliseconds">The milliseconds elapsed since Epoch.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp From(long milliseconds) {
        return new UnixTimestamp(milliseconds);
    }

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from a <see cref="DateTimeOffset"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp From(DateTimeOffset dto) {
        return new UnixTimestamp(dto.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from a <see cref="DateTime"/>. 
    /// </summary>
    /// <remarks>
    /// If the DateTime kind is Local, it is converted to UTC. 
    /// If Unspecified, it is assumed to be UTC.
    /// </remarks>
    public static UnixTimestamp From(DateTime dt) {
        if(dt.Kind == DateTimeKind.Local) {
            dt = dt.ToUniversalTime();
        }
        else if(dt.Kind == DateTimeKind.Unspecified) {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return new UnixTimestamp(((DateTimeOffset)dt).ToUnixTimeMilliseconds());
    }

    // -------------------------------------------------------------------------
    // CONVERSIONS (Instance Methods)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts the timestamp to a UTC <see cref="DateTimeOffset"/>.
    /// </summary>
    public DateTimeOffset ToDateTimeOffset() {
        return DateTimeOffset.FromUnixTimeMilliseconds(_milliseconds);
    }

    /// <summary>
    /// Converts the timestamp to a UTC <see cref="DateTime"/>.
    /// </summary>
    public DateTime ToDateTimeUtc() {
        return ToDateTimeOffset().UtcDateTime;
    }

    /// <summary>
    /// Converts the timestamp to a Local <see cref="DateTime"/>.
    /// </summary>
    public DateTime ToDateTimeLocal() {
        return ToDateTimeOffset().LocalDateTime;
    }

    // -------------------------------------------------------------------------
    // OPERATORS & CASTING
    // -------------------------------------------------------------------------

    /// <summary>Adds a TimeSpan to a UnixTimestamp, returning a new future timestamp.</summary>
    public static UnixTimestamp operator +(UnixTimestamp left, TimeSpan right)
        => new(left._milliseconds + (long)right.TotalMilliseconds);

    /// <summary>Subtracts a TimeSpan from a UnixTimestamp, returning a past timestamp.</summary>
    public static UnixTimestamp operator -(UnixTimestamp left, TimeSpan right)
        => new(left._milliseconds - (long)right.TotalMilliseconds);

    /// <summary>Calculates the time difference between two timestamps.</summary>
    public static TimeSpan operator -(UnixTimestamp left, UnixTimestamp right)
        => TimeSpan.FromMilliseconds(left._milliseconds - right._milliseconds);

    // Comparators
    public static bool operator >(UnixTimestamp left, UnixTimestamp right) => left._milliseconds > right._milliseconds;
    public static bool operator <(UnixTimestamp left, UnixTimestamp right) => left._milliseconds < right._milliseconds;
    public static bool operator >=(UnixTimestamp left, UnixTimestamp right) => left._milliseconds >= right._milliseconds;
    public static bool operator <=(UnixTimestamp left, UnixTimestamp right) => left._milliseconds <= right._milliseconds;

    // Equality with raw long (allows check like: timestamp == 0)
    public static bool operator ==(UnixTimestamp left, long right) => left._milliseconds == right;
    public static bool operator !=(UnixTimestamp left, long right) => left._milliseconds != right;

    public int CompareTo(UnixTimestamp other) => _milliseconds.CompareTo(other._milliseconds);

    // Casting - Primitive
    /// <summary>Implicitly converts to long (milliseconds) to allow easy math operations if needed.</summary>
    public static implicit operator long(UnixTimestamp ts) => ts._milliseconds;

    /// <summary>Explicitly converts long (milliseconds) to UnixTimestamp.</summary>
    public static explicit operator UnixTimestamp(long milliseconds) => new(milliseconds);

    // Casting - DateTimeOffset (NEW ADDITION)
    /// <summary>
    /// Implicitly converts a <see cref="UnixTimestamp"/> to a <see cref="DateTimeOffset"/>.
    /// </summary>
    public static implicit operator DateTimeOffset(UnixTimestamp ts) => ts.ToDateTimeOffset();

    /// <summary>
    /// Explicitly converts a <see cref="DateTimeOffset"/> to a <see cref="UnixTimestamp"/>.
    /// </summary>
    public static explicit operator UnixTimestamp(DateTimeOffset dto) => From(dto);

    // -------------------------------------------------------------------------
    // FORMATTING
    // -------------------------------------------------------------------------

    public override string ToString() {
        return _milliseconds.ToString(CultureInfo.InvariantCulture);
    }

    public string ToString(string? format, IFormatProvider? formatProvider) {
        return _milliseconds.ToString(format, formatProvider);
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return _milliseconds.TryFormat(destination, out charsWritten, format, provider);
    }

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format, IFormatProvider? provider) {
        return _milliseconds.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }

    // -------------------------------------------------------------------------
    // PARSING
    // -------------------------------------------------------------------------

    public static UnixTimestamp Parse(string s) => Parse(s, null);

    public static UnixTimestamp Parse(string s, IFormatProvider? provider) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static UnixTimestamp Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        if(long.TryParse(s, NumberStyles.Integer, provider ?? CultureInfo.InvariantCulture, out long result)) {
            return new UnixTimestamp(result);
        }
        throw new FormatException($"Invalid Unix Timestamp format: '{s.ToString()}'");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UnixTimestamp result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UnixTimestamp result) {
        if(long.TryParse(s, NumberStyles.Integer, provider ?? CultureInfo.InvariantCulture, out long milliseconds)) {
            result = new UnixTimestamp(milliseconds);
            return true;
        }
        result = default;
        return false;
    }

    // UTF-8 Parsing (Optimized for JSON/Web)

    public static UnixTimestamp Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if(Utf8Parser.TryParse(utf8Text, out long result, out _)) {
            return new UnixTimestamp(result);
        }
        throw new FormatException("Invalid UTF-8 sequence for Unix Timestamp.");
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out UnixTimestamp result) {
        if(Utf8Parser.TryParse(utf8Text, out long milliseconds, out _)) {
            result = new UnixTimestamp(milliseconds);
            return true;
        }
        result = default;
        return false;
    }
}

/// <summary>
/// Handles strict and flexible serialization of <see cref="UnixTimestamp"/>.
/// Supports both numeric JSON values and string JSON values.
/// Interprets values as MILLISECONDS.
/// </summary>
public sealed class UnixTimestampJsonConverter : JsonConverter<UnixTimestamp> {

    /// <inheritdoc/>
    public override UnixTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // Optimization: Try reading as a number first (most common case)
        if(reader.TokenType == JsonTokenType.Number) {
            if(reader.TryGetInt64(out long milliseconds)) {
                return UnixTimestamp.From(milliseconds);
            }
        }

        // Fallback: Try reading as a string (common in some APIs to avoid JS number precision issues)
        if(reader.TokenType == JsonTokenType.String) {
            // Read directly from the raw byte span to avoid allocating a string
            ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

            if(UnixTimestamp.TryParse(span, null, out UnixTimestamp result)) {
                return result;
            }
        }

        throw new JsonException($"Unable to convert JSON token {reader.TokenType} to UnixTimestamp.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, UnixTimestamp value, JsonSerializerOptions options) {
        // Standard practice is writing timestamps as numbers.
        writer.WriteNumberValue(value.Milliseconds);
    }

    /// <inheritdoc/>
    public override UnixTimestamp ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        if(UnixTimestamp.TryParse(span, null, out UnixTimestamp result)) {
            return result;
        }
        throw new JsonException($"Invalid property name format for UnixTimestamp.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, UnixTimestamp value, JsonSerializerOptions options) {
        // Property keys in JSON must always be strings
        writer.WritePropertyName(value.ToString());
    }
}