using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Represents a validated name identifier for a Bloom Filter instance.
/// </summary>
[DebuggerDisplay("{Value,nq}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(FilterNameJsonConverter))]
public readonly record struct FilterName :
    IEquatable<FilterName>,
    IComparable<FilterName>,
    IComparable,
    ISpanParsable<FilterName>,
    IUtf8SpanParsable<FilterName>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<FilterName, FilterName, bool>,
    IComparisonOperators<FilterName, FilterName, bool> {

    /// <summary>
    /// The maximum allowed character length for a filter name.
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>
    /// The maximum allowed byte length when parsing from UTF-8.
    /// </summary>
    public const int MaxUtf8Length = 512;

    private static readonly SearchValues<char> AllowedChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.");

    private static readonly SearchValues<byte> AllowedUtf8Bytes =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_."u8);

    private readonly string? _value;

    /// <summary>
    /// An empty <see cref="FilterName"/> instance.
    /// </summary>
    public static readonly FilterName Empty = default;

    /// <summary>
    /// Gets a value indicating whether the name is uninitialized or empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    /// <summary>
    /// Gets the number of characters in the name.
    /// </summary>
    public int Length => this._value?.Length ?? 0;

    /// <summary>
    /// Gets the underlying string value. Returns <see cref="string.Empty"/> if uninitialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FilterName(string value) {
        this._value = value;
    }

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A valid <see cref="FilterName"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is empty or exceeds <see cref="MaxLength"/>.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> contains disallowed characters.</exception>
    public static FilterName Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="FilterName"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is empty or exceeds <see cref="MaxLength"/>.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> contains disallowed characters.</exception>
    public static FilterName Parse(ReadOnlySpan<char> s) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(s));
        }

        if(trimmed.Length > MaxLength) {
            throw new ArgumentException($"Filter name length cannot exceed {MaxLength} characters.", nameof(s));
        }

        if(trimmed.IndexOfAnyExcept(AllowedChars) >= 0) {
            throw new FormatException($"Filter name '{trimmed}' contains invalid characters. Allowed characters: a-z, A-Z, 0-9, '-', '_', '.'");
        }

        return new FilterName(trimmed.ToString());
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A valid <see cref="FilterName"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="utf8Text"/> is empty, exceeds <see cref="MaxUtf8Length"/>, or exceeds <see cref="MaxLength"/> characters.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="utf8Text"/> contains disallowed characters.</exception>
    public static FilterName Parse(ReadOnlySpan<byte> utf8Text) {
        ReadOnlySpan<byte> trimmed = TrimUtf8(utf8Text);
        if(trimmed.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(utf8Text));
        }

        if(trimmed.Length is > MaxUtf8Length or > MaxLength) {
            throw new ArgumentException($"Filter name length cannot exceed {MaxLength} characters.", nameof(utf8Text));
        }

        if(trimmed.IndexOfAnyExcept(AllowedUtf8Bytes) >= 0) {
            throw new FormatException("Filter name contains invalid characters. Allowed characters: a-z, A-Z, 0-9, '-', '_', '.'");
        }

        return new FilterName(System.Text.Encoding.ASCII.GetString(trimmed));
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">The resulting <see cref="FilterName"/> if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out FilterName result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = Empty;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a character span into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">The resulting <see cref="FilterName"/> if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out FilterName result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty || trimmed.Length > MaxLength || trimmed.IndexOfAnyExcept(AllowedChars) >= 0) {
            result = Empty;
            return false;
        }

        result = new FilterName(trimmed.ToString());
        return true;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 byte span into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">The resulting <see cref="FilterName"/> if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out FilterName result) {
        ReadOnlySpan<byte> trimmed = TrimUtf8(utf8Text);
        if(trimmed.IsEmpty || trimmed.Length > MaxUtf8Length || trimmed.Length > MaxLength || trimmed.IndexOfAnyExcept(AllowedUtf8Bytes) >= 0) {
            result = Empty;
            return false;
        }

        result = new FilterName(System.Text.Encoding.ASCII.GetString(trimmed));
        return true;
    }

    #endregion

    #region Formatting

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <summary>
    /// Returns the name as a read-only character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> AsSpan() {
        return this.Value.AsSpan();
    }

    /// <summary>
    /// Attempts to format the name into the destination character span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="true"/> if the write was successful; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            charsWritten = 0;
            return true;
        }

        ReadOnlySpan<char> source = this.Value.AsSpan();
        if(destination.Length < source.Length) {
            charsWritten = 0;
            return false;
        }

        source.CopyTo(destination);
        charsWritten = source.Length;
        return true;
    }

    /// <summary>
    /// Attempts to format the name into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination byte span.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the write was successful; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            bytesWritten = 0;
            return true;
        }

        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(utf8Destination, out bytesWritten);
    }

    static FilterName IParsable<FilterName>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<FilterName>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FilterName result) {
        return TryParse(s, out result);
    }

    static FilterName ISpanParsable<FilterName>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<FilterName>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FilterName result) {
        return TryParse(s, out result);
    }

    static FilterName IUtf8SpanParsable<FilterName>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<FilterName>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out FilterName result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region Equality and Ordering

    /// <inheritdoc/>
    public bool Equals(FilterName other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return string.GetHashCode(this.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public int CompareTo(FilterName other) {
        return string.Compare(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is FilterName other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(FilterName)}.", nameof(obj));
    }

    /// <inheritdoc/>
    public static bool operator <(FilterName left, FilterName right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc/>
    public static bool operator <=(FilterName left, FilterName right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc/>
    public static bool operator >(FilterName left, FilterName right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc/>
    public static bool operator >=(FilterName left, FilterName right) {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Implicitly converts a string to a <see cref="FilterName"/>.
    /// </summary>
    public static implicit operator FilterName(string value) {
        return Parse(value);
    }

    /// <summary>
    /// Implicitly converts a <see cref="FilterName"/> to a string.
    /// </summary>
    public static implicit operator string(FilterName name) {
        return name.Value;
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> TrimUtf8(ReadOnlySpan<byte> span) {
        int start = 0;
        while(start < span.Length && (span[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')) {
            start++;
        }

        int end = span.Length - 1;
        while(end >= start && (span[end] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')) {
            end--;
        }

        return span.Slice(start, end - start + 1);
    }
}

/// <summary>
/// Converts a <see cref="FilterName"/> to or from JSON.
/// </summary>
public sealed class FilterNameJsonConverter : JsonConverter<FilterName> {
    /// <inheritdoc/>
    public override FilterName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected string token for {nameof(FilterName)}, but received {reader.TokenType}.");
        }

        ReadOnlySpan<byte> utf8Span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        if(FilterName.TryParse(utf8Span, out FilterName result)) {
            return result;
        }

        throw new JsonException($"Failed to parse '{reader.GetString()}' as a valid {nameof(FilterName)}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FilterName value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override FilterName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        ReadOnlySpan<byte> utf8Span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        if(FilterName.TryParse(utf8Span, out FilterName result)) {
            return result;
        }

        throw new JsonException($"Failed to parse property name '{reader.GetString()}' as a valid {nameof(FilterName)}.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, FilterName value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}