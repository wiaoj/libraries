using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Represents a strongly-typed, validated key for a distributed counter.
/// Prevents primitive obsession and ensures consistent key formatting.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(CounterKeyJsonConverter))]
public readonly record struct CounterKey :
    IEquatable<CounterKey>,
    IComparable<CounterKey>,
    IComparable,
    ISpanParsable<CounterKey>,
    IUtf8SpanParsable<CounterKey>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string? _value;

    /// <summary>
    /// Gets the underlying string representation of the key, or an empty string if uninitialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the key is null or empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    /// <summary>
    /// Represents an empty counter key.
    /// </summary>
    public static CounterKey Empty => default;

    /// <summary>
    /// Gets a default equality comparer for <see cref="CounterKey"/> using ordinal comparison with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<CounterKey> OrdinalComparer { get; } = new OrdinalEqualityComparer();

    /// <summary>
    /// Gets an equality comparer for <see cref="CounterKey"/> using case-insensitive ordinal comparison with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<CounterKey> OrdinalIgnoreCaseComparer { get; } = new OrdinalIgnoreCaseEqualityComparer();

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterKey"/> struct.
    /// </summary>
    /// <param name="value">The key value. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public CounterKey(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value.Trim();
    }

    private CounterKey(string value, bool skipValidation) {
        this._value = skipValidation ? value : (value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Parses a string into a <see cref="CounterKey"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A validated <see cref="CounterKey"/>.</returns>
    public static CounterKey Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new CounterKey(s);
    }

    /// <summary>
    /// Parses a character span into a <see cref="CounterKey"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A validated <see cref="CounterKey"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is empty or whitespace.</exception>
    public static CounterKey Parse(ReadOnlySpan<char> s) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            throw new ArgumentException("Key cannot be empty or consist only of whitespace.", nameof(s));
        }
        return new CounterKey(trimmed.ToString(), skipValidation: true);
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into a <see cref="CounterKey"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A validated <see cref="CounterKey"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the byte span is empty or invalid UTF-8.</exception>
    public static CounterKey Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out CounterKey result)) {
            throw new ArgumentException("Key cannot be empty, whitespace, or invalid UTF-8.", nameof(utf8Text));
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="CounterKey"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out CounterKey result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }

        result = new CounterKey(s);
        return true;
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="CounterKey"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out CounterKey result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            result = default;
            return false;
        }

        result = new CounterKey(trimmed.ToString(), skipValidation: true);
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into a <see cref="CounterKey"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out CounterKey result) {
        ReadOnlySpan<byte> trimmed = utf8Text.Trim(" \t\n\r"u8);
        if(trimmed.IsEmpty) {
            result = default;
            return false;
        }

        try {
            string str = Encoding.UTF8.GetString(trimmed);
            result = new CounterKey(str, skipValidation: true);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Formats the key into the specified character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>
    /// Formats the key into the specified UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    /// <inheritdoc/>
    public bool Equals(CounterKey other) =>
        string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <inheritdoc/>
    public int CompareTo(CounterKey other) =>
        string.CompareOrdinal(this.Value, other.Value);

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is CounterKey other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(CounterKey)}.", nameof(obj));
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        this.Value;

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(utf8Destination, out bytesWritten);

    static CounterKey IParsable<CounterKey>.Parse(string s, IFormatProvider? provider) =>
        Parse(s);

    static bool IParsable<CounterKey>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CounterKey result) =>
        TryParse(s, out result);

    static CounterKey ISpanParsable<CounterKey>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        Parse(s);

    static bool ISpanParsable<CounterKey>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CounterKey result) =>
        TryParse(s, out result);

    static CounterKey IUtf8SpanParsable<CounterKey>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(utf8Text);

    static bool IUtf8SpanParsable<CounterKey>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CounterKey result) =>
        TryParse(utf8Text, out result);

    /// <summary>
    /// Implicitly converts a string to a <see cref="CounterKey"/>.
    /// </summary>
    public static implicit operator CounterKey(string s) => Parse(s);

    /// <summary>
    /// Implicitly converts a <see cref="CounterKey"/> to its string value.
    /// </summary>
    public static implicit operator string(CounterKey k) => k.Value;

    private sealed class OrdinalEqualityComparer :
        IEqualityComparer<CounterKey>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, CounterKey> {
        public bool Equals(CounterKey x, CounterKey y) =>
            string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(CounterKey obj) =>
            StringComparer.Ordinal.GetHashCode(obj.Value);

        public bool Equals(ReadOnlySpan<char> alternate, CounterKey other) =>
            alternate.Equals(other.Value.AsSpan(), StringComparison.Ordinal);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.Ordinal);

        public CounterKey Create(ReadOnlySpan<char> alternate) =>
            CounterKey.Parse(alternate);
    }

    private sealed class OrdinalIgnoreCaseEqualityComparer :
        IEqualityComparer<CounterKey>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, CounterKey> {
        public bool Equals(CounterKey x, CounterKey y) =>
            string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(CounterKey obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);

        public bool Equals(ReadOnlySpan<char> alternate, CounterKey other) =>
            alternate.Equals(other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public CounterKey Create(ReadOnlySpan<char> alternate) =>
            CounterKey.Parse(alternate);
    }
}

/// <summary>
/// Custom JSON converter for <see cref="CounterKey"/> ensuring direct string serialization and dictionary key support.
/// </summary>
public sealed class CounterKeyJsonConverter : JsonConverter<CounterKey> {
    /// <inheritdoc/>
    public override CounterKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? value = reader.GetString();
        return value is null ? default : CounterKey.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CounterKey value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override CounterKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return CounterKey.Parse(reader.GetString()!);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, CounterKey value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}