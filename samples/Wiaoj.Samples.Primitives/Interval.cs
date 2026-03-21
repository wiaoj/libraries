using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Specifies whether an interval endpoint is included or excluded.
/// </summary>
public enum IntervalBoundary : byte {
    /// <summary>The endpoint value is included in the interval. Uses [ or ] notation.</summary>
    Closed = 0,
    /// <summary>The endpoint value is excluded from the interval. Uses ( or ) notation.</summary>
    Open = 1,
}

/// <summary>
/// Represents a mathematical interval [start, end] with configurable open/closed bounds.
/// </summary>
/// <typeparam name="T">
/// The type of the interval endpoints. Must be comparable.
/// Common use cases: <see cref="int"/>, <see cref="double"/>, <see cref="DateTime"/>,
/// <see cref="DateTimeOffset"/>, <see cref="UnixTimestamp"/>.
/// </typeparam>
/// <remarks>
/// <para>Notation:</para>
/// <list type="bullet">
///   <item>[a, b] — both endpoints included (Closed, Closed)</item>
///   <item>(a, b) — both endpoints excluded (Open, Open)</item>
///   <item>[a, b) — left included, right excluded (Closed, Open) — most common in date ranges</item>
///   <item>(a, b] — left excluded, right included (Open, Closed)</item>
/// </list>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(IntervalJsonConverterFactory))]
public readonly record struct Interval<T> :
    IEquatable<Interval<T>>
    where T : IComparable<T> {

    /// <summary>Gets the start (lower bound) of the interval.</summary>
    public T Start { get; }

    /// <summary>Gets the end (upper bound) of the interval.</summary>
    public T End { get; }

    /// <summary>Gets whether the start endpoint is open or closed.</summary>
    public IntervalBoundary StartBoundary { get; }

    /// <summary>Gets whether the end endpoint is open or closed.</summary>
    public IntervalBoundary EndBoundary { get; }

    /// <summary>Gets whether the start endpoint is included.</summary>
    public bool IsStartClosed => StartBoundary == IntervalBoundary.Closed;

    /// <summary>Gets whether the end endpoint is included.</summary>
    public bool IsEndClosed => EndBoundary == IntervalBoundary.Closed;

    private Interval(T start, T end, IntervalBoundary startBoundary, IntervalBoundary endBoundary) {
        Start = start;
        End = end;
        StartBoundary = startBoundary;
        EndBoundary = endBoundary;
    }

    #region Factory

    /// <summary>
    /// Creates a closed interval [start, end] — both endpoints included.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="start"/> &gt; <paramref name="end"/>.</exception>
    public static Interval<T> Closed(T start, T end) {
        Validate(start, end);
        return new(start, end, IntervalBoundary.Closed, IntervalBoundary.Closed);
    }

    /// <summary>
    /// Creates an open interval (start, end) — both endpoints excluded.
    /// </summary>
    public static Interval<T> Open(T start, T end) {
        Validate(start, end);
        return new(start, end, IntervalBoundary.Open, IntervalBoundary.Open);
    }

    /// <summary>
    /// Creates a half-open interval [start, end) — start included, end excluded.
    /// Most common for date ranges (e.g., [2024-01-01, 2025-01-01)).
    /// </summary>
    public static Interval<T> ClosedOpen(T start, T end) {
        Validate(start, end);
        return new(start, end, IntervalBoundary.Closed, IntervalBoundary.Open);
    }

    /// <summary>
    /// Creates a half-open interval (start, end] — start excluded, end included.
    /// </summary>
    public static Interval<T> OpenClosed(T start, T end) {
        Validate(start, end);
        return new(start, end, IntervalBoundary.Open, IntervalBoundary.Closed);
    }

    /// <summary>
    /// Creates an interval with explicitly specified boundaries.
    /// </summary>
    public static Interval<T> Create(T start, T end, IntervalBoundary startBoundary, IntervalBoundary endBoundary) {
        Validate(start, end);
        return new(start, end, startBoundary, endBoundary);
    }

    /// <summary>Tries to create an interval without throwing.</summary>
    public static bool TryCreate(T start, T end, IntervalBoundary startBoundary, IntervalBoundary endBoundary,
                                 out Interval<T> result) {
        if(start.CompareTo(end) > 0) { result = default; return false; }
        result = new(start, end, startBoundary, endBoundary);
        return true;
    }

    private static void Validate(T start, T end) {
        if(start.CompareTo(end) > 0)
            throw new ArgumentException(
                $"Start ({start}) must be less than or equal to End ({end}).", nameof(start));
    }

    #endregion

    #region Contains

    /// <summary>
    /// Determines whether the specified value is within this interval.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is within the interval; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T value) {
        int cmpStart = value.CompareTo(Start);
        int cmpEnd = value.CompareTo(End);

        bool afterStart = StartBoundary == IntervalBoundary.Closed ? cmpStart >= 0 : cmpStart > 0;
        bool beforeEnd = EndBoundary == IntervalBoundary.Closed ? cmpEnd <= 0 : cmpEnd < 0;

        return afterStart && beforeEnd;
    }

    #endregion

    #region Set Operations

    /// <summary>
    /// Determines whether this interval overlaps with another.
    /// Two intervals overlap if they share at least one common point.
    /// </summary>
    public bool Overlaps(Interval<T> other) {
        // They don't overlap only if one ends before the other starts.
        bool thisEndsBeforeOtherStarts = CompareEndToStart(this, other) < 0;
        bool otherEndsBeforeThisStarts = CompareEndToStart(other, this) < 0;
        return !thisEndsBeforeOtherStarts && !otherEndsBeforeThisStarts;
    }

    /// <summary>
    /// Determines whether this interval is entirely contained within another.
    /// </summary>
    public bool IsSubsetOf(Interval<T> other) =>
        other.Contains(Start) && other.Contains(End);

    /// <summary>
    /// Tries to compute the intersection of this interval with another.
    /// Returns <see langword="false"/> if they don't overlap.
    /// </summary>
    public bool TryIntersect(Interval<T> other, out Interval<T> intersection) {
        if(!Overlaps(other)) { intersection = default; return false; }

        T newStart = Start.CompareTo(other.Start) >= 0 ? Start : other.Start;
        T newEnd = End.CompareTo(other.End) <= 0 ? End : other.End;

        // Use the tighter boundary at each end
        IntervalBoundary newStartBoundary = Start.CompareTo(other.Start) == 0
            ? (StartBoundary == IntervalBoundary.Open || other.StartBoundary == IntervalBoundary.Open
                ? IntervalBoundary.Open : IntervalBoundary.Closed)
            : (Start.CompareTo(other.Start) > 0 ? StartBoundary : other.StartBoundary);

        IntervalBoundary newEndBoundary = End.CompareTo(other.End) == 0
            ? (EndBoundary == IntervalBoundary.Open || other.EndBoundary == IntervalBoundary.Open
                ? IntervalBoundary.Open : IntervalBoundary.Closed)
            : (End.CompareTo(other.End) < 0 ? EndBoundary : other.EndBoundary);

        intersection = new(newStart, newEnd, newStartBoundary, newEndBoundary);
        return true;
    }

    private static int CompareEndToStart(Interval<T> a, Interval<T> b) {
        int cmp = a.End.CompareTo(b.Start);
        if(cmp != 0) return cmp;
        // Equal values: open boundary means strictly excluded → no overlap at this point
        if(a.EndBoundary == IntervalBoundary.Open || b.StartBoundary == IntervalBoundary.Open)
            return -1;
        return 0;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns <see langword="true"/> if the interval is empty
    /// (i.e., start == end with at least one open boundary).
    /// </summary>
    public bool IsEmpty {
        get {
            int cmp = Start.CompareTo(End);
            return cmp == 0 && (StartBoundary == IntervalBoundary.Open || EndBoundary == IntervalBoundary.Open);
        }
    }

    #endregion

    #region Formatting

    /// <summary>
    /// Returns the interval in mathematical notation.
    /// Examples: "[1, 10]", "(0.0, 1.0)", "[2024-01-01, 2025-01-01)"
    /// </summary>
    public override string ToString() {
        char open = IsStartClosed ? '[' : '(';
        char close = IsEndClosed ? ']' : ')';
        return $"{open}{Start}, {End}{close}";
    }

    #endregion

    #region Equality

    /// <inheritdoc/>
    public bool Equals(Interval<T> other) =>
        Start.CompareTo(other.Start) == 0 &&
        End.CompareTo(other.End) == 0 &&
        StartBoundary == other.StartBoundary &&
        EndBoundary == other.EndBoundary;

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Start, End, StartBoundary, EndBoundary);

    #endregion

    /// <summary>Deconstructs into start and end.</summary>
    public void Deconstruct(out T start, out T end) { start = Start; end = End; }
}

/// <summary>Factory for creating JSON converters for <see cref="Interval{T}"/>.</summary>
public sealed class IntervalJsonConverterFactory : JsonConverterFactory {
    public override bool CanConvert(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Interval<>);

    public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o) {
        Type converterType = typeof(IntervalJsonConverter<>).MakeGenericType(t.GetGenericArguments());
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>JSON converter for <see cref="Interval{T}"/>.</summary>
public sealed class IntervalJsonConverter<T> : JsonConverter<Interval<T>> where T : IComparable<T> {
    public override Interval<T> Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        T start = JsonSerializer.Deserialize<T>(root.GetProperty("start").GetRawText(), o)!;
        T end = JsonSerializer.Deserialize<T>(root.GetProperty("end").GetRawText(), o)!;
        bool startOpen = root.TryGetProperty("startOpen", out JsonElement sOpen) && sOpen.GetBoolean();
        bool endOpen = root.TryGetProperty("endOpen", out JsonElement eOpen) && eOpen.GetBoolean();

        return Interval<T>.Create(
            start, end,
            startOpen ? IntervalBoundary.Open : IntervalBoundary.Closed,
            endOpen ? IntervalBoundary.Open : IntervalBoundary.Closed
        );
    }

    public override void Write(Utf8JsonWriter writer, Interval<T> value, JsonSerializerOptions o) {
        writer.WriteStartObject();
        writer.WritePropertyName("start"); JsonSerializer.Serialize(writer, value.Start, o);
        writer.WritePropertyName("end"); JsonSerializer.Serialize(writer, value.End, o);
        writer.WriteBoolean("startOpen", value.StartBoundary == IntervalBoundary.Open);
        writer.WriteBoolean("endOpen", value.EndBoundary == IntervalBoundary.Open);
        writer.WriteEndObject();
    }
}