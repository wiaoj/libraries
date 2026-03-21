using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents an integer that is strictly greater than zero (1, 2, 3, ...).
/// </summary>
/// <remarks>
/// Use for quantities, counts, page sizes, retry counts — anywhere "zero or negative
/// makes no sense." Once constructed, no further range checks are needed downstream.
/// </remarks>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(PositiveIntJsonConverter))]
public readonly record struct PositiveInt :
    IEquatable<PositiveInt>,
    IComparable<PositiveInt>,
    ISpanParsable<PositiveInt>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IAdditionOperators<PositiveInt, PositiveInt, PositiveInt>,
    IMultiplyOperators<PositiveInt, PositiveInt, PositiveInt>,
    IComparisonOperators<PositiveInt, PositiveInt, bool> {

    /// <summary>Gets the underlying integer value (always &gt; 0).</summary>
    public int Value { get; }

    /// <summary>The smallest possible <see cref="PositiveInt"/> (value = 1).</summary>
    public static PositiveInt One { get; } = new(1);

    private PositiveInt(int value) => Value = value;

    #region Factory

    /// <summary>Creates a <see cref="PositiveInt"/> from an integer.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is &lt;= 0.</exception>
    public static PositiveInt Create(int value) {
        if(value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be greater than zero.");
        return new(value);
    }

    /// <summary>Tries to create a <see cref="PositiveInt"/> without throwing.</summary>
    public static bool TryCreate(int value, out PositiveInt result) {
        if(value <= 0) { result = default; return false; }
        result = new(value);
        return true;
    }

    #endregion

    #region Arithmetic

    /// <summary>Adds two positive integers. Result is always positive.</summary>
    public static PositiveInt operator +(PositiveInt l, PositiveInt r) => new(l.Value + r.Value);

    /// <summary>Multiplies two positive integers. Result is always positive.</summary>
    public static PositiveInt operator *(PositiveInt l, PositiveInt r) => new(l.Value * r.Value);

    #endregion

    #region Parsing

    public static PositiveInt Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    public static PositiveInt Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out PositiveInt r)) return r;
        throw new FormatException($"'{s}' is not a valid PositiveInt (must be integer > 0).");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out PositiveInt result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out PositiveInt result) {
        if(!int.TryParse(s, out int v) || v <= 0) { result = default; return false; }
        result = new(v);
        return true;
    }

    static PositiveInt IParsable<PositiveInt>.Parse(string s, IFormatProvider? p) => Parse(s);
    static bool IParsable<PositiveInt>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, out PositiveInt r) => TryParse(s, out r);
    static PositiveInt ISpanParsable<PositiveInt>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) => Parse(s);
    static bool ISpanParsable<PositiveInt>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out PositiveInt r) => TryParse(s, out r);

    #endregion

    #region Formatting

    public override string ToString() => Value.ToString();
    string IFormattable.ToString(string? f, IFormatProvider? p) => Value.ToString(f, p);

    bool ISpanFormattable.TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p)
        => Value.TryFormat(dest, out written, format, p);

    bool IUtf8SpanFormattable.TryFormat(Span<byte> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p)
        => Value.TryFormat(dest, out written, format, p);

    #endregion

    #region Comparison & Equality

    public int CompareTo(PositiveInt other) => Value.CompareTo(other.Value);
    public bool Equals(PositiveInt other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator >(PositiveInt l, PositiveInt r) => l.Value > r.Value;
    public static bool operator <(PositiveInt l, PositiveInt r) => l.Value < r.Value;
    public static bool operator >=(PositiveInt l, PositiveInt r) => l.Value >= r.Value;
    public static bool operator <=(PositiveInt l, PositiveInt r) => l.Value <= r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(PositiveInt p) => p.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator PositiveInt(int v) => Create(v);

    #endregion
}

/// <summary>
/// Represents an integer that is zero or greater (0, 1, 2, ...).
/// </summary>
/// <remarks>
/// Use for indices, offsets, counters — anywhere "negative makes no sense"
/// but zero is a valid state (e.g., zero items in a cart, page offset 0).
/// </remarks>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(NonNegativeIntJsonConverter))]
public readonly record struct NonNegativeInt :
    IEquatable<NonNegativeInt>,
    IComparable<NonNegativeInt>,
    ISpanParsable<NonNegativeInt>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IAdditionOperators<NonNegativeInt, NonNegativeInt, NonNegativeInt>,
    IComparisonOperators<NonNegativeInt, NonNegativeInt, bool> {

    /// <summary>Gets the underlying integer value (always &gt;= 0).</summary>
    public int Value { get; }

    /// <summary>Represents zero.</summary>
    public static NonNegativeInt Zero { get; } = new(0);

    private NonNegativeInt(int value) => Value = value;

    #region Factory

    /// <summary>Creates a <see cref="NonNegativeInt"/> from an integer.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is negative.</exception>
    public static NonNegativeInt Create(int value) {
        if(value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be zero or greater.");
        return new(value);
    }

    /// <summary>Tries to create a <see cref="NonNegativeInt"/> without throwing.</summary>
    public static bool TryCreate(int value, out NonNegativeInt result) {
        if(value < 0) { result = default; return false; }
        result = new(value);
        return true;
    }

    /// <summary>Converts a <see cref="PositiveInt"/> to <see cref="NonNegativeInt"/> (always safe).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NonNegativeInt From(PositiveInt p) => new(p.Value);

    #endregion

    #region Arithmetic

    /// <summary>Adds two non-negative integers. Result is always non-negative.</summary>
    public static NonNegativeInt operator +(NonNegativeInt l, NonNegativeInt r) => new(l.Value + r.Value);

    /// <summary>
    /// Subtracts two non-negative integers, clamped to zero.
    /// Unlike raw int subtraction, this cannot go negative.
    /// </summary>
    public static NonNegativeInt operator -(NonNegativeInt l, NonNegativeInt r) =>
        new(Math.Max(0, l.Value - r.Value));

    #endregion

    #region Parsing

    public static NonNegativeInt Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    public static NonNegativeInt Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out NonNegativeInt r)) return r;
        throw new FormatException($"'{s}' is not a valid NonNegativeInt (must be integer >= 0).");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out NonNegativeInt result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out NonNegativeInt result) {
        if(!int.TryParse(s, out int v) || v < 0) { result = default; return false; }
        result = new(v);
        return true;
    }

    static NonNegativeInt IParsable<NonNegativeInt>.Parse(string s, IFormatProvider? p) => Parse(s);
    static bool IParsable<NonNegativeInt>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, out NonNegativeInt r) => TryParse(s, out r);
    static NonNegativeInt ISpanParsable<NonNegativeInt>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) => Parse(s);
    static bool ISpanParsable<NonNegativeInt>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out NonNegativeInt r) => TryParse(s, out r);

    #endregion

    #region Formatting

    public override string ToString() => Value.ToString();
    string IFormattable.ToString(string? f, IFormatProvider? p) => Value.ToString(f, p);
    bool ISpanFormattable.TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p)
        => Value.TryFormat(dest, out written, format, p);
    bool IUtf8SpanFormattable.TryFormat(Span<byte> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p)
        => Value.TryFormat(dest, out written, format, p);

    #endregion

    #region Comparison & Equality

    public int CompareTo(NonNegativeInt other) => Value.CompareTo(other.Value);
    public bool Equals(NonNegativeInt other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator >(NonNegativeInt l, NonNegativeInt r) => l.Value > r.Value;
    public static bool operator <(NonNegativeInt l, NonNegativeInt r) => l.Value < r.Value;
    public static bool operator >=(NonNegativeInt l, NonNegativeInt r) => l.Value >= r.Value;
    public static bool operator <=(NonNegativeInt l, NonNegativeInt r) => l.Value <= r.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(NonNegativeInt n) => n.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator NonNegativeInt(int v) => Create(v);

    // PositiveInt → NonNegativeInt widening (safe, no info lost)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NonNegativeInt(PositiveInt p) => From(p);

    #endregion
}

public sealed class PositiveIntJsonConverter : JsonConverter<PositiveInt> {
    public override PositiveInt Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) {
        int v = reader.GetInt32();
        if(!PositiveInt.TryCreate(v, out PositiveInt r))
            throw new JsonException($"Expected integer > 0, got {v}.");
        return r;
    }
    public override void Write(Utf8JsonWriter writer, PositiveInt value, JsonSerializerOptions o)
        => writer.WriteNumberValue(value.Value);
}

public sealed class NonNegativeIntJsonConverter : JsonConverter<NonNegativeInt> {
    public override NonNegativeInt Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) {
        int v = reader.GetInt32();
        if(!NonNegativeInt.TryCreate(v, out NonNegativeInt r))
            throw new JsonException($"Expected integer >= 0, got {v}.");
        return r;
    }
    public override void Write(Utf8JsonWriter writer, NonNegativeInt value, JsonSerializerOptions o)
        => writer.WriteNumberValue(value.Value);
}