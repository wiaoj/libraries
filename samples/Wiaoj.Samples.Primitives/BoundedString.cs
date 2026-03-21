using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Marker interface for compile-time integer constants used as generic bounds.
/// </summary>
/// <remarks>
/// Implement this on a zero-size struct to encode an integer as a type parameter:
/// <code>
/// public readonly struct Min3  : IIntConstant { public static int Value => 3;  }
/// public readonly struct Max50 : IIntConstant { public static int Value => 50; }
/// </code>
/// </remarks>
public interface IIntConstant {
    /// <summary>The constant integer value.</summary>
    static abstract int Value { get; }
}

// ── Common constant structs — ready to use out of the box ──────────────────

public readonly struct C1 : IIntConstant { public static int Value => 1; }
public readonly struct C2 : IIntConstant { public static int Value => 2; }
public readonly struct C3 : IIntConstant { public static int Value => 3; }
public readonly struct C4 : IIntConstant { public static int Value => 4; }
public readonly struct C5 : IIntConstant { public static int Value => 5; }
public readonly struct C6 : IIntConstant { public static int Value => 6; }
public readonly struct C8 : IIntConstant { public static int Value => 8; }
public readonly struct C10 : IIntConstant { public static int Value => 10; }
public readonly struct C12 : IIntConstant { public static int Value => 12; }
public readonly struct C16 : IIntConstant { public static int Value => 16; }
public readonly struct C20 : IIntConstant { public static int Value => 20; }
public readonly struct C32 : IIntConstant { public static int Value => 32; }
public readonly struct C50 : IIntConstant { public static int Value => 50; }
public readonly struct C64 : IIntConstant { public static int Value => 64; }
public readonly struct C100 : IIntConstant { public static int Value => 100; }
public readonly struct C128 : IIntConstant { public static int Value => 128; }
public readonly struct C255 : IIntConstant { public static int Value => 255; }
public readonly struct C256 : IIntConstant { public static int Value => 256; }
public readonly struct C512 : IIntConstant { public static int Value => 512; }

/// <summary>
/// A strongly-typed string whose length is guaranteed to be within [<typeparamref name="TMin"/>, <typeparamref name="TMax"/>].
/// </summary>
/// <typeparam name="TMin">
/// A struct implementing <see cref="IIntConstant"/> that specifies the minimum length (inclusive).
/// Use the built-in constants (<see cref="C1"/>, <see cref="C3"/>, etc.) or define your own.
/// </typeparam>
/// <typeparam name="TMax">
/// A struct implementing <see cref="IIntConstant"/> that specifies the maximum length (inclusive).
/// </typeparam>
/// <example>
/// <code>
/// // Username: 3–50 characters
/// BoundedString&lt;C3, C50&gt; username = BoundedString&lt;C3, C50&gt;.Create("alice");
///
/// // Or define a named alias:
/// public readonly struct Username {
///     public BoundedString&lt;C3, C50&gt; Value { get; }
///     public Username(string s) => Value = BoundedString&lt;C3, C50&gt;.Create(s);
/// }
/// </code>
/// </example>
[DebuggerDisplay("{ToString(),nq} (len={Length})")]
[JsonConverter(typeof(BoundedStringJsonConverterFactory))]
public readonly record struct BoundedString<TMin, TMax> :
    IEquatable<BoundedString<TMin, TMax>>,
    IComparable<BoundedString<TMin, TMax>>,
    ISpanParsable<BoundedString<TMin, TMax>>,
    ISpanFormattable,
    IUtf8SpanFormattable
    where TMin : struct, IIntConstant
    where TMax : struct, IIntConstant {

    private static readonly int MinLength = TMin.Value;
    private static readonly int MaxLength = TMax.Value;

    private readonly string _value;

    /// <summary>Gets the underlying string value.</summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>Gets the length of the string.</summary>
    public int Length => this.Value.Length;

    /// <summary>Gets the minimum allowed length for this type.</summary>
    public static int AllowedMinLength => MinLength;

    /// <summary>Gets the maximum allowed length for this type.</summary>
    public static int AllowedMaxLength => MaxLength;

    private BoundedString(string value) {
        // Validate bounds at type-load time — catches impossible types like BoundedString<C50, C3>
        if(MinLength < 0) throw new InvalidOperationException($"TMin.Value must be >= 0, got {MinLength}.");
        if(MaxLength < MinLength) throw new InvalidOperationException($"TMax.Value ({MaxLength}) must be >= TMin.Value ({MinLength}).");
        _value = value;
    }

    #region Factory

    /// <summary>
    /// Creates a <see cref="BoundedString{TMin,TMax}"/> from the given string.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the length is outside [TMin, TMax].</exception>
    public static BoundedString<TMin, TMax> Create(string value) {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value.AsSpan(), value.Length);
        return new(value);
    }

    /// <summary>Tries to create a <see cref="BoundedString{TMin,TMax}"/> without throwing.</summary>
    public static bool TryCreate([NotNullWhen(true)] string? value, out BoundedString<TMin, TMax> result) {
        if(value is null || !IsLengthValid(value.Length)) { result = default; return false; }
        result = new(value);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLengthValid(int length) => length >= MinLength && length <= MaxLength;

    private static void Validate(ReadOnlySpan<char> span, int length) {
        if(length < MinLength)
            throw new ArgumentException(
                $"Value is too short: {length} characters (minimum is {MinLength}).");
        if(length > MaxLength)
            throw new ArgumentException(
                $"Value is too long: {length} characters (maximum is {MaxLength}).");
    }

    #endregion

    #region Parsing

    /// <summary>Parses a string into a <see cref="BoundedString{TMin,TMax}"/>.</summary>
    public static BoundedString<TMin, TMax> Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>Parses a character span into a <see cref="BoundedString{TMin,TMax}"/>.</summary>
    public static BoundedString<TMin, TMax> Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out BoundedString<TMin, TMax> r)) return r;
        throw new FormatException(
            $"String length {s.Length} is outside the allowed range [{MinLength}, {MaxLength}].");
    }

    /// <summary>Tries to parse a string into a <see cref="BoundedString{TMin,TMax}"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out BoundedString<TMin, TMax> result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a character span into a <see cref="BoundedString{TMin,TMax}"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out BoundedString<TMin, TMax> result) {
        if(!IsLengthValid(s.Length)) { result = default; return false; }
        result = new(s.ToString());
        return true;
    }

    static BoundedString<TMin, TMax> IParsable<BoundedString<TMin, TMax>>.Parse(string s, IFormatProvider? p) => Parse(s);
    static bool IParsable<BoundedString<TMin, TMax>>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, out BoundedString<TMin, TMax> r) => TryParse(s, out r);
    static BoundedString<TMin, TMax> ISpanParsable<BoundedString<TMin, TMax>>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) => Parse(s);
    static bool ISpanParsable<BoundedString<TMin, TMax>>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out BoundedString<TMin, TMax> r) => TryParse(s, out r);

    #endregion

    #region Formatting

    /// <inheritdoc/>
    public override string ToString() => this.Value;

    string IFormattable.ToString(string? format, IFormatProvider? p) => this.Value;

    bool ISpanFormattable.TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p) {
        ReadOnlySpan<char> src = this.Value.AsSpan();
        if(dest.Length < src.Length) { written = 0; return false; }
        src.CopyTo(dest);
        written = src.Length;
        return true;
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p) {
        if(string.IsNullOrEmpty(_value)) { written = 0; return true; }
        if(utf8Dest.Length < _value.Length) { written = 0; return false; }
        written = System.Text.Encoding.UTF8.GetBytes(_value.AsSpan(), utf8Dest);
        return true;
    }

    #endregion

    #region Comparison & Operators

    /// <inheritdoc/>
    public int CompareTo(BoundedString<TMin, TMax> other) =>
        string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public bool Equals(BoundedString<TMin, TMax> other) =>
        string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Value.GetHashCode(StringComparison.Ordinal);

    public static bool operator >(BoundedString<TMin, TMax> l, BoundedString<TMin, TMax> r) => l.CompareTo(r) > 0;
    public static bool operator <(BoundedString<TMin, TMax> l, BoundedString<TMin, TMax> r) => l.CompareTo(r) < 0;
    public static bool operator >=(BoundedString<TMin, TMax> l, BoundedString<TMin, TMax> r) => l.CompareTo(r) >= 0;
    public static bool operator <=(BoundedString<TMin, TMax> l, BoundedString<TMin, TMax> r) => l.CompareTo(r) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(BoundedString<TMin, TMax> s) => s.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BoundedString<TMin, TMax>(string s) => Create(s);

    #endregion
}

/// <summary>Factory for creating JSON converters for <see cref="BoundedString{TMin,TMax}"/>.</summary>
public sealed class BoundedStringJsonConverterFactory : JsonConverterFactory {
    public override bool CanConvert(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BoundedString<,>);

    public override JsonConverter CreateConverter(Type t, JsonSerializerOptions o) {
        Type converterType = typeof(BoundedStringJsonConverter<,>).MakeGenericType(t.GetGenericArguments());
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>JSON converter for <see cref="BoundedString{TMin,TMax}"/>.</summary>
public sealed class BoundedStringJsonConverter<TMin, TMax> : JsonConverter<BoundedString<TMin, TMax>>
    where TMin : struct, IIntConstant
    where TMax : struct, IIntConstant {

    public override BoundedString<TMin, TMax> Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) {
        string? s = reader.GetString();
        if(s is not null && BoundedString<TMin, TMax>.TryCreate(s, out var result)) return result;
        throw new JsonException(
            $"String length is outside the allowed range [{TMin.Value}, {TMax.Value}].");
    }

    public override void Write(Utf8JsonWriter writer, BoundedString<TMin, TMax> value, JsonSerializerOptions o)
        => writer.WriteStringValue(value.Value);
}