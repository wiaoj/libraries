using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a string that is guaranteed to be non-null, non-empty, and non-whitespace.
/// </summary>
/// <remarks>
/// Eliminates the most common string guard pattern:
/// <code>if (string.IsNullOrWhiteSpace(value)) throw ...</code>
/// Once you have a <see cref="NonEmptyString"/>, you never need to check again.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(NonEmptyStringJsonConverter))]
public readonly record struct NonEmptyString :
    IEquatable<NonEmptyString>,
    IComparable<NonEmptyString>,
    ISpanParsable<NonEmptyString>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>Gets the underlying string value.</summary>
    public string Value => this._value ?? throw new InvalidOperationException("NonEmptyString was not initialized.");

    /// <summary>Gets the length of the string.</summary>
    public int Length => this.Value.Length;

    private NonEmptyString(string value) => this._value = value;

    #region Factory

    /// <summary>
    /// Creates a <see cref="NonEmptyString"/> from the given string.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is empty or whitespace.</exception>
    public static NonEmptyString Create(string value) {
        ArgumentNullException.ThrowIfNull(value);
        if(string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", nameof(value));
        return new NonEmptyString(value);
    }

    /// <summary>Tries to create a <see cref="NonEmptyString"/> without throwing.</summary>
    public static bool TryCreate([NotNullWhen(true)] string? value, out NonEmptyString result) {
        if(string.IsNullOrWhiteSpace(value)) { result = default; return false; }
        result = new NonEmptyString(value);
        return true;
    }

    #endregion

    #region Parsing

    /// <summary>Parses a string into a <see cref="NonEmptyString"/>.</summary>
    public static NonEmptyString Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>Parses a character span into a <see cref="NonEmptyString"/>.</summary>
    public static NonEmptyString Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out NonEmptyString result)) return result;
        throw new FormatException("Value cannot be empty or whitespace.");
    }

    /// <summary>Tries to parse a string into a <see cref="NonEmptyString"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out NonEmptyString result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a character span into a <see cref="NonEmptyString"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out NonEmptyString result) {
        if(s.IsWhiteSpace() || s.IsEmpty) { result = default; return false; }
        result = new NonEmptyString(s.ToString());
        return true;
    }

    static NonEmptyString IParsable<NonEmptyString>.Parse(string s, IFormatProvider? p) => Parse(s);
    static bool IParsable<NonEmptyString>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, out NonEmptyString r) => TryParse(s, out r);
    static NonEmptyString ISpanParsable<NonEmptyString>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) => Parse(s);
    static bool ISpanParsable<NonEmptyString>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out NonEmptyString r) => TryParse(s, out r);

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
        if(utf8Dest.Length < this._value.Length) { written = 0; return false; }
        written = System.Text.Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Dest);
        return true;
    }

    #endregion

    #region Comparison & Operators

    /// <inheritdoc/>
    public int CompareTo(NonEmptyString other) =>
        string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public bool Equals(NonEmptyString other) =>
        string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Value.GetHashCode(StringComparison.Ordinal);

    public static bool operator >(NonEmptyString l, NonEmptyString r) => l.CompareTo(r) > 0;
    public static bool operator <(NonEmptyString l, NonEmptyString r) => l.CompareTo(r) < 0;
    public static bool operator >=(NonEmptyString l, NonEmptyString r) => l.CompareTo(r) >= 0;
    public static bool operator <=(NonEmptyString l, NonEmptyString r) => l.CompareTo(r) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(NonEmptyString s) => s.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator NonEmptyString(string s) => Create(s);

    #endregion
}

public sealed class NonEmptyStringJsonConverter : JsonConverter<NonEmptyString> {
    public override NonEmptyString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? s = reader.GetString();
        if(NonEmptyString.TryCreate(s, out NonEmptyString result)) return result;
        throw new JsonException("Expected a non-empty, non-whitespace string.");
    }
    public override void Write(Utf8JsonWriter writer, NonEmptyString value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

