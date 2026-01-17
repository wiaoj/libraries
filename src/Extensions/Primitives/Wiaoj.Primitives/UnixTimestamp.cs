using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a Unix Timestamp in MILLISECONDS (milliseconds elapsed since 1970-01-01 00:00:00 UTC).
/// This value object wraps a <see cref="long"/> to ensure type safety, eliminates "primitive obsession",
/// and provides high-performance parsing/formatting capabilities without overhead.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Design Philosophy:</strong> This struct hides complex formatting interfaces (like <see cref="IFormatProvider"/>) 
/// from the public API to keep usage simple, as Unix Timestamps are fundamentally raw integers invariant of culture.
/// However, it strictly implements modern .NET interfaces (<see cref="ISpanParsable{TSelf}"/>, <see cref="ISpanFormattable"/>, etc.) 
/// explicitly to ensure compatibility with generic math, JSON serializers, and Minimal APIs.
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
    /// <value>The milliseconds as a <see cref="long"/>.</value>
    public long Milliseconds => this._milliseconds;

    // Private constructor to enforce creation via static factory methods.
    private UnixTimestamp(long milliseconds) {
        this._milliseconds = milliseconds;
    }

    // -------------------------------------------------------------------------
    // FACTORIES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the current Unix timestamp based on <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    /// <value>A <see cref="UnixTimestamp"/> representing the current moment.</value>
    public static UnixTimestamp Now => From(DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from raw milliseconds.
    /// </summary>
    /// <param name="milliseconds">The milliseconds elapsed since Epoch.</param>
    /// <returns>A new <see cref="UnixTimestamp"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp From(long milliseconds) {
        return new UnixTimestamp(milliseconds);
    }

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="dateTimeOffset">The date time offset to convert.</param>
    /// <returns>A new <see cref="UnixTimestamp"/> corresponding to the UTC instant of the input.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp From(DateTimeOffset dateTimeOffset) {
        return new UnixTimestamp(dateTimeOffset.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from a <see cref="DateTime"/>. 
    /// </summary>
    /// <param name="dateTime">The date time to convert.</param>
    /// <returns>A new <see cref="UnixTimestamp"/>.</returns>
    /// <remarks>
    /// If the <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Local"/>, it is converted to UTC. 
    /// If <see cref="DateTimeKind.Unspecified"/>, it is assumed to be UTC.
    /// </remarks>
    public static UnixTimestamp From(DateTime dateTime) {
        if(dateTime.Kind == DateTimeKind.Local) {
            dateTime = dateTime.ToUniversalTime();
        }
        else if(dateTime.Kind == DateTimeKind.Unspecified) {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        return new UnixTimestamp(((DateTimeOffset)dateTime).ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Creates a <see cref="UnixTimestamp"/> from a <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider to get the current UTC instant from.</param>
    /// <returns>A new <see cref="UnixTimestamp"/> representing the current time in UTC.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp From(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);

        return new UnixTimestamp(timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
    }

    // -------------------------------------------------------------------------
    // CONVERSIONS (Instance Methods)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts the timestamp to a UTC <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>A UTC DateTimeOffset.</returns>
    public DateTimeOffset ToDateTimeOffset() {
        return DateTimeOffset.FromUnixTimeMilliseconds(this._milliseconds);
    }

    /// <summary>
    /// Converts the timestamp to a UTC <see cref="DateTime"/>.
    /// </summary>
    /// <returns>A UTC DateTime.</returns>
    public DateTime ToDateTimeUtc() {
        return ToDateTimeOffset().UtcDateTime;
    }

    /// <summary>
    /// Converts the timestamp to a Local <see cref="DateTime"/>.
    /// </summary>
    /// <returns>A Local DateTime.</returns>
    public DateTime ToDateTimeLocal() {
        return ToDateTimeOffset().LocalDateTime;
    }

    // -------------------------------------------------------------------------
    // OPERATORS & CASTING
    // -------------------------------------------------------------------------

    /// <summary>Adds a <see cref="TimeSpan"/> to a <see cref="UnixTimestamp"/>, returning a new future timestamp.</summary>
    public static UnixTimestamp operator +(UnixTimestamp left, TimeSpan right) {
        return new(left._milliseconds + (long)right.TotalMilliseconds);
    }

    /// <summary>Subtracts a <see cref="TimeSpan"/> from a <see cref="UnixTimestamp"/>, returning a past timestamp.</summary>
    public static UnixTimestamp operator -(UnixTimestamp left, TimeSpan right) {
        return new(left._milliseconds - (long)right.TotalMilliseconds);
    }

    /// <summary>Calculates the time difference between two timestamps.</summary>
    public static TimeSpan operator -(UnixTimestamp left, UnixTimestamp right) {
        return TimeSpan.FromMilliseconds(left._milliseconds - right._milliseconds);
    }

    // Comparators
    /// <inheritdoc/>
    public static bool operator >(UnixTimestamp left, UnixTimestamp right) {
        return left._milliseconds > right._milliseconds;
    }

    /// <inheritdoc/>
    public static bool operator <(UnixTimestamp left, UnixTimestamp right) {
        return left._milliseconds < right._milliseconds;
    }

    /// <inheritdoc/>
    public static bool operator >=(UnixTimestamp left, UnixTimestamp right) {
        return left._milliseconds >= right._milliseconds;
    }

    /// <inheritdoc/>
    public static bool operator <=(UnixTimestamp left, UnixTimestamp right) {
        return left._milliseconds <= right._milliseconds;
    }

    // Equality with raw long (allows check like: timestamp == 0)
    /// <summary>Checks equality between a timestamp and raw milliseconds.</summary>
    public static bool operator ==(UnixTimestamp left, long right) {
        return left._milliseconds == right;
    }

    /// <summary>Checks inequality between a timestamp and raw milliseconds.</summary>
    public static bool operator !=(UnixTimestamp left, long right) {
        return left._milliseconds != right;
    }

    /// <inheritdoc/>
    public int CompareTo(UnixTimestamp other) {
        return this._milliseconds.CompareTo(other._milliseconds);
    }

    // Casting - Primitive
    /// <summary>Implicitly converts to <see cref="long"/> (milliseconds) to allow easy math operations if needed.</summary>
    public static implicit operator long(UnixTimestamp ts) {
        return ts._milliseconds;
    }

    /// <summary>Explicitly converts <see cref="long"/> (milliseconds) to <see cref="UnixTimestamp"/>.</summary>
    public static explicit operator UnixTimestamp(long milliseconds) {
        return new(milliseconds);
    }

    // Casting - DateTimeOffset
    /// <summary>Implicitly converts a <see cref="UnixTimestamp"/> to a <see cref="DateTimeOffset"/>.</summary>
    public static implicit operator DateTimeOffset(UnixTimestamp ts) {
        return ts.ToDateTimeOffset();
    }

    /// <summary>Explicitly converts a <see cref="DateTimeOffset"/> to a <see cref="UnixTimestamp"/>.</summary>
    public static explicit operator UnixTimestamp(DateTimeOffset dto) {
        return From(dto);
    }

    // -------------------------------------------------------------------------
    // FORMATTING (Public Convenience & Explicit Interfaces)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the string representation of the timestamp (milliseconds) using <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <returns>A string containing the raw milliseconds (e.g., "1672531200000").</returns>
    public override string ToString() {
        return this._milliseconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this._milliseconds.ToString(format, formatProvider);
    }

    /// <inheritdoc/>
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this._milliseconds.TryFormat(destination, out charsWritten, format, provider);
    }

    /// <inheritdoc/>
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this._milliseconds.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }

    // -------------------------------------------------------------------------
    // PARSING (Public Convenience & Explicit Interfaces)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a string containing raw milliseconds into a <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The parsed timestamp.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if the string is not a valid integer.</exception>
    public static UnixTimestamp Parse(string s) {
        Preca.ThrowIfNull(s);
        return ParseInternal(s.AsSpan(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a character span containing raw milliseconds into a <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <returns>The parsed timestamp.</returns>
    /// <exception cref="FormatException">Thrown if the span is not a valid integer.</exception>
    public static UnixTimestamp Parse(ReadOnlySpan<char> s) {
        // Convenience method always uses InvariantCulture
        return ParseInternal(s, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tries to parse a string containing raw milliseconds into a <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed timestamp if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out UnixTimestamp result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParseInternal(s.AsSpan(), CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a character span containing raw milliseconds into a <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <param name="s">The span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed timestamp if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out UnixTimestamp result) {
        return TryParseInternal(s, CultureInfo.InvariantCulture, out result);
    }

    // --- Internal Logic Reuse ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UnixTimestamp ParseInternal(ReadOnlySpan<char> s, IFormatProvider? provider) {
        if(long.TryParse(s, NumberStyles.Integer, provider, out long result)) {
            return new UnixTimestamp(result);
        }
        throw new FormatException($"Invalid Unix Timestamp format: '{s}'");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInternal(ReadOnlySpan<char> s, IFormatProvider? provider, out UnixTimestamp result) {
        if(long.TryParse(s, NumberStyles.Integer, provider, out long milliseconds)) {
            result = new UnixTimestamp(milliseconds);
            return true;
        }
        result = default;
        return false;
    }

    // --- Explicit Interface Implementations (IParsable, ISpanParsable) ---

    /// <inheritdoc/>
    static UnixTimestamp IParsable<UnixTimestamp>.Parse(string s, IFormatProvider? provider) {
        Preca.ThrowIfNull(s);
        return ParseInternal(s.AsSpan(), provider);
    }

    /// <inheritdoc/>
    static bool IParsable<UnixTimestamp>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UnixTimestamp result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParseInternal(s.AsSpan(), provider, out result);
    }

    /// <inheritdoc/>
    static UnixTimestamp ISpanParsable<UnixTimestamp>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return ParseInternal(s, provider);
    }

    /// <inheritdoc/>
    static bool ISpanParsable<UnixTimestamp>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UnixTimestamp result) {
        return TryParseInternal(s, provider, out result);
    }

    // --- Explicit Interface Implementations (UTF-8) ---

    /// <inheritdoc/>
    static UnixTimestamp IUtf8SpanParsable<UnixTimestamp>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if(Utf8Parser.TryParse(utf8Text, out long result, out _)) {
            return new UnixTimestamp(result);
        }
        throw new FormatException("Invalid UTF-8 sequence for Unix Timestamp.");
    }

    /// <inheritdoc/>
    static bool IUtf8SpanParsable<UnixTimestamp>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out UnixTimestamp result) {
        if(Utf8Parser.TryParse(utf8Text, out long milliseconds, out _)) {
            result = new UnixTimestamp(milliseconds);
            return true;
        }
        result = default;
        return false;
    }
}