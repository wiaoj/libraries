using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.BloomFilter;
/// <summary>
/// Represents a strongly-typed, validated name for a Bloom Filter.
/// Ensures the name contains only safe characters (alphanumeric, hyphens, underscores, dots) and does not exceed length limits.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(FilterNameJsonConverter))]
public readonly record struct FilterName :
    IEquatable<FilterName>,
    IComparable<FilterName>,
    ISpanParsable<FilterName> {

    // Allowed characters: a-z, A-Z, 0-9, hyphen (-), underscore (_), dot (.).
    private static readonly SearchValues<char> AllowedChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.");

    private const int MaxLength = 128;

    private readonly string _value;

    /// <summary>
    /// Gets the underlying string value. Returns an empty string if the struct is default.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    // Private constructor to enforce validation via Parse methods.
    private FilterName(string value) {
        this._value = value;
    }

    /// <summary>
    /// Parses and validates a string into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>A valid <see cref="FilterName"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the input is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the input is empty or too long.</exception>
    /// <exception cref="FormatException">Thrown if the input contains invalid characters.</exception>
    public static FilterName Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses and validates a character span into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="FilterName"/> instance.</returns>
    public static FilterName Parse(ReadOnlySpan<char> s) {
        if(s.IsEmpty)
            throw new ArgumentException("Filter name cannot be empty.");

        if(s.Length > MaxLength)
            throw new ArgumentException($"Filter name too long. Max: {MaxLength}, Current: {s.Length}");

        // Validates characters using vectorized search for performance
        if(s.IndexOfAnyExcept(AllowedChars) >= 0) {
            throw new FormatException($"Filter name '{s}' contains invalid characters. Only alphanumeric, '-', '_' and '.' are allowed.");
        }

        return new FilterName(s.ToString());
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="FilterName"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">The resulting <see cref="FilterName"/> if successful.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out FilterName result) {
        if(s.IsEmpty || s.Length > MaxLength || s.IndexOfAnyExcept(AllowedChars) >= 0) {
            result = default;
            return false;
        }
        result = new FilterName(s.ToString());
        return true;
    }

    // --- Implicit Operators ---

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

    // --- Standard Overrides ---

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public int CompareTo(FilterName other) {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    // --- ISpanParsable Implementation ---

    static FilterName IParsable<FilterName>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<FilterName>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FilterName result) {
        return TryParse(s.AsSpan(), out result);
    }

    static FilterName ISpanParsable<FilterName>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<FilterName>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FilterName result) {
        return TryParse(s, out result);
    }
}

/// <summary>
/// A custom JSON converter for <see cref="FilterName"/> to serialize/deserialize it as a simple string.
/// </summary>
public sealed class FilterNameJsonConverter : JsonConverter<FilterName> {
    public override FilterName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? s = reader.GetString();
        return s is null ? default : FilterName.Parse(s);
    }

    public override void Write(Utf8JsonWriter writer, FilterName value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    public override FilterName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return FilterName.Parse(reader.GetString()!);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, FilterName value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}