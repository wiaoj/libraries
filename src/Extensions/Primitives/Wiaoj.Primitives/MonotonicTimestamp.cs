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
/// Represents a high-resolution, monotonic point in time based on the operating system's performance counter.
/// Monotonic timestamps strictly move forward and are completely immune to system wall-clock skew, 
/// leap seconds, daylight saving changes, and NTP corrections.
/// </summary>
/// <remarks>
/// <para>
/// <b>Intended Usage:</b> This type is designed exclusively for process-internal operations such as 
/// delayed schedulers, rate limiters, circuit breakers, cache TTLs, and elapsed time measurements.
/// </para>
/// <para>
/// <b>Ephemerality:</b> Monotonic timestamps are ephemeral and tied to the current machine's boot lifecycle. 
/// They should not be stored permanently across application restarts or shared across different machines.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(MonotonicTimestampJsonConverter))]
public readonly record struct MonotonicTimestamp :
    IComparable<MonotonicTimestamp>,
    IComparable,
    IEquatable<MonotonicTimestamp>,
    ISpanParsable<MonotonicTimestamp>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanParsable<MonotonicTimestamp>,
    IUtf8SpanFormattable,
    IAdditionOperators<MonotonicTimestamp, TimeSpan, MonotonicTimestamp>,
    ISubtractionOperators<MonotonicTimestamp, TimeSpan, MonotonicTimestamp>,
    ISubtractionOperators<MonotonicTimestamp, MonotonicTimestamp, TimeSpan>,
    IComparisonOperators<MonotonicTimestamp, MonotonicTimestamp, bool> {

    // -------------------------------------------------------------------------
    // CONSTANTS & FIELDS
    // -------------------------------------------------------------------------

    private readonly long _ticks;

    /// <summary>
    /// Represents an empty or uninitialized monotonic timestamp (0 ticks).
    /// </summary>
    public static MonotonicTimestamp Empty { get; } = new(0);

    /// <summary>
    /// Represents the zero monotonic point in time (0 ticks).
    /// </summary>
    public static MonotonicTimestamp Zero { get; } = new(0);

    /// <summary>
    /// Represents the minimum representable monotonic timestamp.
    /// </summary>
    public static MonotonicTimestamp MinValue { get; } = new(0);

    /// <summary>
    /// Represents the maximum representable monotonic timestamp.
    /// </summary>
    public static MonotonicTimestamp MaxValue { get; } = new(long.MaxValue);

    /// <summary>
    /// Gets the raw monotonic tick count.
    /// </summary>
    /// <value>The raw ticks as a <see cref="long"/>.</value>
    public long RawTicks => this._ticks;

    /// <summary>
    /// Gets a value indicating whether this instance represents an empty or uninitialized timestamp (0 ticks).
    /// </summary>
    public bool IsEmpty => this._ticks == 0;

    /// <summary>
    /// Gets the timestamp frequency (ticks per second) of the current system.
    /// </summary>
    public static long Frequency => Stopwatch.Frequency;

    private MonotonicTimestamp(long ticks) {
        this._ticks = ticks;
    }

    // -------------------------------------------------------------------------
    // FACTORIES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets a <see cref="MonotonicTimestamp"/> representing the current instant.
    /// </summary>
    public static MonotonicTimestamp Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Creates a <see cref="MonotonicTimestamp"/> from a raw tick count.
    /// </summary>
    /// <param name="ticks">The raw performance counter ticks.</param>
    /// <returns>A new <see cref="MonotonicTimestamp"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonotonicTimestamp FromRawTicks(long ticks) {
        return new MonotonicTimestamp(ticks);
    }

    /// <summary>
    /// Creates a <see cref="MonotonicTimestamp"/> using the provided <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <returns>A new <see cref="MonotonicTimestamp"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonotonicTimestamp From(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        return new MonotonicTimestamp(timeProvider.GetTimestamp());
    }

    // -------------------------------------------------------------------------
    // FLUENT ARITHMETIC
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a <see cref="TimeSpan"/> duration to this timestamp.
    /// </summary>
    /// <param name="timeSpan">The duration to add.</param>
    /// <returns>A future <see cref="MonotonicTimestamp"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MonotonicTimestamp Add(TimeSpan timeSpan) {
        return this + timeSpan;
    }

    /// <summary>
    /// Subtracts a <see cref="TimeSpan"/> duration from this timestamp.
    /// </summary>
    /// <param name="timeSpan">The duration to subtract.</param>
    /// <returns>A past <see cref="MonotonicTimestamp"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MonotonicTimestamp Subtract(TimeSpan timeSpan) {
        return this - timeSpan;
    }

    /// <summary>
    /// Adds the specified number of milliseconds to this instance.
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds to add.</param>
    /// <returns>A new <see cref="MonotonicTimestamp"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MonotonicTimestamp AddMilliseconds(double milliseconds) {
        return this + TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// Adds the specified number of seconds to this instance.
    /// </summary>
    /// <param name="seconds">The number of seconds to add.</param>
    /// <returns>A new <see cref="MonotonicTimestamp"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MonotonicTimestamp AddSeconds(double seconds) {
        return this + TimeSpan.FromSeconds(seconds);
    }

    // -------------------------------------------------------------------------
    // DOMAIN LOGIC & COMPARISON HELPERS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calculates the time duration elapsed since this timestamp until the current moment.
    /// </summary>
    /// <returns>A <see cref="TimeSpan"/> representing the elapsed duration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ElapsedSinceNow() {
        return Stopwatch.GetElapsedTime(this._ticks);
    }

    /// <summary>
    /// Calculates the time duration elapsed since this timestamp until the instant provided by <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider to read the ending timestamp from.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the elapsed duration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ElapsedSince(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        return timeProvider.GetElapsedTime(this._ticks);
    }

    /// <summary>
    /// Calculates the time duration elapsed between this timestamp and a subsequent timestamp.
    /// </summary>
    /// <param name="endTimestamp">The subsequent timestamp.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the elapsed duration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ElapsedUntil(MonotonicTimestamp endTimestamp) {
        return Stopwatch.GetElapsedTime(this._ticks, endTimestamp._ticks);
    }

    /// <summary>
    /// Checks whether this timestamp falls within a specific range.
    /// </summary>
    /// <param name="start">The inclusive start timestamp.</param>
    /// <param name="end">The inclusive end timestamp.</param>
    /// <returns><see langword="true"/> if the current timestamp is between the start and end (inclusive); otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBetween(MonotonicTimestamp start, MonotonicTimestamp end) {
        return this._ticks >= start._ticks && this._ticks <= end._ticks;
    }

    /// <summary>
    /// Checks if this timestamp is strictly before another timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBefore(MonotonicTimestamp other) {
        return this._ticks < other._ticks;
    }

    /// <summary>
    /// Checks if this timestamp is strictly after another timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAfter(MonotonicTimestamp other) {
        return this._ticks > other._ticks;
    }

    /// <summary>
    /// Checks whether this timestamp represents an instant that has already passed relative to <see cref="Now"/>.
    /// </summary>
    public bool HasPassed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Stopwatch.GetTimestamp() >= this._ticks;
    }

    /// <summary>
    /// Checks whether this timestamp represents a future instant relative to <see cref="Now"/>.
    /// </summary>
    public bool IsFuture {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Stopwatch.GetTimestamp() < this._ticks;
    }

    // -------------------------------------------------------------------------
    // OPERATORS & CASTING
    // -------------------------------------------------------------------------

    /// <inheritdoc cref="IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonotonicTimestamp operator +(MonotonicTimestamp left, TimeSpan right) {
        long deltaTicks = (long)(right.Ticks * ((double)Stopwatch.Frequency / TimeSpan.TicksPerSecond));
        return new MonotonicTimestamp(left._ticks + deltaTicks);
    }

    /// <inheritdoc cref="ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonotonicTimestamp operator -(MonotonicTimestamp left, TimeSpan right) {
        long deltaTicks = (long)(right.Ticks * ((double)Stopwatch.Frequency / TimeSpan.TicksPerSecond));
        return new MonotonicTimestamp(left._ticks - deltaTicks);
    }

    /// <inheritdoc cref="ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan operator -(MonotonicTimestamp left, MonotonicTimestamp right) {
        return Stopwatch.GetElapsedTime(right._ticks, left._ticks);
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(MonotonicTimestamp left, MonotonicTimestamp right) {
        return left._ticks > right._ticks;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(MonotonicTimestamp left, MonotonicTimestamp right) {
        return left._ticks < right._ticks;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(MonotonicTimestamp left, MonotonicTimestamp right) {
        return left._ticks >= right._ticks;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(MonotonicTimestamp left, MonotonicTimestamp right) {
        return left._ticks <= right._ticks;
    }

    /// <inheritdoc/>
    public int CompareTo(MonotonicTimestamp other) {
        return this._ticks.CompareTo(other._ticks);
    }

    /// <inheritdoc/>
    int IComparable.CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is MonotonicTimestamp other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(MonotonicTimestamp)}.", nameof(obj));
    }

    // Casting - Primitive
    /// <summary>Explicitly converts a <see cref="MonotonicTimestamp"/> to raw <see cref="long"/> ticks.</summary>
    public static explicit operator long(MonotonicTimestamp ts) {
        return ts._ticks;
    }

    /// <summary>Explicitly converts raw <see cref="long"/> ticks to a <see cref="MonotonicTimestamp"/>.</summary>
    public static explicit operator MonotonicTimestamp(long ticks) {
        return new(ticks);
    }

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <inheritdoc/>
    public override string ToString() {
        return this._ticks.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider = null) {
        return this._ticks.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) {
        return this._ticks.TryFormat(destination, out charsWritten, format, provider ?? CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) {
        return this._ticks.TryFormat(utf8Destination, out bytesWritten, format, provider ?? CultureInfo.InvariantCulture);
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string containing raw ticks into a <see cref="MonotonicTimestamp"/>.
    /// </summary>
    public static MonotonicTimestamp Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span containing raw ticks into a <see cref="MonotonicTimestamp"/>.
    /// </summary>
    public static MonotonicTimestamp Parse(ReadOnlySpan<char> s) {
        if(long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)) {
            return new MonotonicTimestamp(result);
        }
        throw new FormatException($"Invalid Monotonic Timestamp format: '{s}'");
    }

    /// <summary>
    /// Parses a UTF-8 byte span containing raw ticks into a <see cref="MonotonicTimestamp"/>.
    /// </summary>
    public static MonotonicTimestamp Parse(ReadOnlySpan<byte> utf8Text) {
        if(Utf8Parser.TryParse(utf8Text, out long result, out int bytesConsumed) && bytesConsumed == utf8Text.Length) {
            return new MonotonicTimestamp(result);
        }
        throw new FormatException("Invalid UTF-8 sequence for Monotonic Timestamp.");
    }

    /// <summary>
    /// Tries to parse a string containing raw ticks into a <see cref="MonotonicTimestamp"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out MonotonicTimestamp result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span containing raw ticks into a <see cref="MonotonicTimestamp"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out MonotonicTimestamp result) {
        if(long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)) {
            result = new MonotonicTimestamp(ticks);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span containing raw ticks into a <see cref="MonotonicTimestamp"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out MonotonicTimestamp result) {
        if(Utf8Parser.TryParse(utf8Text, out long ticks, out int bytesConsumed) && bytesConsumed == utf8Text.Length) {
            result = new MonotonicTimestamp(ticks);
            return true;
        }
        result = default;
        return false;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static MonotonicTimestamp IParsable<MonotonicTimestamp>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<MonotonicTimestamp>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out MonotonicTimestamp result) {
        return TryParse(s, out result);
    }

    static MonotonicTimestamp ISpanParsable<MonotonicTimestamp>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<MonotonicTimestamp>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out MonotonicTimestamp result) {
        return TryParse(s, out result);
    }

    static MonotonicTimestamp IUtf8SpanParsable<MonotonicTimestamp>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<MonotonicTimestamp>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out MonotonicTimestamp result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs equality comparisons on <see cref="MonotonicTimestamp"/>
    /// and supports zero-allocation alternate lookups using character spans (<see cref="ReadOnlySpan{Char}"/>) 
    /// and UTF-8 byte spans (<see cref="ReadOnlySpan{Byte}"/>).
    /// </summary>
    public static IEqualityComparer<MonotonicTimestamp> OrdinalComparer => MonotonicTimestampOrdinalComparer.Instance;

    private sealed class MonotonicTimestampOrdinalComparer :
        IEqualityComparer<MonotonicTimestamp>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, MonotonicTimestamp>,
        IAlternateEqualityComparer<ReadOnlySpan<byte>, MonotonicTimestamp> {

        public static MonotonicTimestampOrdinalComparer Instance { get; } = new();

        public bool Equals(MonotonicTimestamp x, MonotonicTimestamp y) {
            return x._ticks == y._ticks;
        }

        public int GetHashCode(MonotonicTimestamp obj) {
            return obj._ticks.GetHashCode();
        }

        // Alternate: ReadOnlySpan<char>
        public bool Equals(ReadOnlySpan<char> alternate, MonotonicTimestamp other) {
            if(long.TryParse(alternate, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)) {
                return ticks == other._ticks;
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(long.TryParse(alternate, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)) {
                return ticks.GetHashCode();
            }
            return 0;
        }

        public MonotonicTimestamp Create(ReadOnlySpan<char> alternate) {
            return MonotonicTimestamp.Parse(alternate);
        }

        // Alternate: ReadOnlySpan<byte> (UTF-8)
        public bool Equals(ReadOnlySpan<byte> alternate, MonotonicTimestamp other) {
            if(Utf8Parser.TryParse(alternate, out long ticks, out int consumed) && consumed == alternate.Length) {
                return ticks == other._ticks;
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<byte> alternate) {
            if(Utf8Parser.TryParse(alternate, out long ticks, out int consumed) && consumed == alternate.Length) {
                return ticks.GetHashCode();
            }
            return 0;
        }

        public MonotonicTimestamp Create(ReadOnlySpan<byte> alternate) {
            return MonotonicTimestamp.Parse(alternate);
        }
    }

    #endregion
}