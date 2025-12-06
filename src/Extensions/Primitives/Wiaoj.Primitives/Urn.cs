using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a Uniform Resource Name (URN) conforming to RFC 8141.
/// Format: urn:<nid>:<nss> (e.g., urn:isbn:978-0-123-45678-9 or urn:user:123456789)
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(UrnJsonConverter))]
public readonly record struct Urn :
    IEquatable<Urn>,
    ISpanParsable<Urn>,
    ISpanFormattable {

    private readonly string _value;

    // Parsed segments (Allocation-free accessors logic can be added if needed, 
    // but storing the full string is safer for a persistent ValueObject)

    /// <summary>
    /// Gets the Namespace Identifier (NID). E.g., "user" in "urn:user:123".
    /// </summary>
    public ReadOnlySpan<char> Namespace => ParseSegment(0);

    /// <summary>
    /// Gets the Namespace Specific String (NSS). E.g., "123" in "urn:user:123".
    /// </summary>
    public ReadOnlySpan<char> Identity => ParseSegment(1);

    /// <summary>
    /// Represents an empty URN.
    /// </summary>
    public static Urn Empty { get; } = new(string.Empty);

    /// <summary>
    /// Gets the full URN string value.
    /// </summary>
    public string Value => _value ?? string.Empty;

    private Urn(string value) {
        _value = value;
    }

    /// <summary>
    /// Deconstructs the URN into its Namespace Identifier (NID) and Namespace Specific String (NSS).
    /// </summary>
    /// <param name="nid">The Namespace Identifier (NID) part of the URN.</param>
    /// <param name="nss">The Namespace Specific String (NSS) part of the URN.</param>
    public void Deconstruct(out ReadOnlySpan<char> nid, out ReadOnlySpan<char> nss) {
        nid = this.Namespace;
        nss = this.Identity;
    }

    #region Factory Methods

    /// <summary>
    /// Creates a URN from a namespace and a string identifier.
    /// </summary>
    /// <example>Urn.Create("user", "12345") -> "urn:user:12345"</example>
    public static Urn Create(string nid, string nss) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        Preca.ThrowIfNullOrWhiteSpace(nss);
        ValidateNid(nid);

        // String interpolation is optimized in modern .NET
        return new Urn($"urn:{nid}:{nss}");
    }

    /// <summary>
    /// Creates a URN from a namespace and a SnowflakeId.
    /// </summary>
    /// <example>Urn.Create("order", id) -> "urn:order:123456789"</example>
    public static Urn Create(string nid, SnowflakeId id) {                       
        return Create(nid, id.ToString());
    }

    /// <summary>
    /// Creates a URN from a namespace and a Guid.
    /// </summary>
    /// <example>Urn.Create("session", guid) -> "urn:session:550e8400-e29b..."</example>
    public static Urn Create(string nid, Guid id) {
        return Create(nid, id.ToString());
    }

    #endregion

    #region Parsing

    public static Urn Parse(string s, IFormatProvider? provider = null) {
        if (TryParse(s, provider, out var result))
            return result;
        throw new FormatException($"Invalid URN format: '{s}'. Expected 'urn:<nid>:<nss>'.");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Urn result) {
        if (string.IsNullOrEmpty(s)) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static Urn Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null) {
        if (TryParse(s, provider, out var result))
            return result;
        throw new FormatException($"Invalid URN format: '{s}'.");
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Urn result) {
        // 1. Check Prefix "urn:" (Case-insensitive)
        if (s.Length < 5 || !s.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)) {
            result = default;
            return false;
        }

        // 2. Check for second colon separator
        // "urn:" is 4 chars. We search after that.
        int secondColon = s.Slice(4).IndexOf(':');
        if (secondColon < 1) { // Namespace must not be empty
            result = default;
            return false;
        }

        // Adjust index relative to original span
        secondColon += 4;

        // 3. Check if NSS (Identity) part exists
        if (secondColon >= s.Length - 1) {
            result = default;
            return false;
        }

        // 4. Validate NID characters (Alpha-numeric and hyphens mostly)
        ReadOnlySpan<char> nid = s.Slice(4, secondColon - 4);
        foreach (char c in nid) {
            if (!IsAlphaNumericOrHyphen(c)) {
                result = default;
                return false;
            }
        }

        result = new Urn(s.ToString());
        return true;
    }

    #endregion

    #region Helpers & Formatting

    private ReadOnlySpan<char> ParseSegment(int segmentIndex) {
        if (string.IsNullOrEmpty(_value))
            return [];

        // segment 0 = NID ("urn:[NID]:nss")
        // segment 1 = NSS ("urn:nid:[NSS]")

        int firstColon = 3; // "urn:" ends at 3
        int secondColon = _value.IndexOf(':', firstColon + 1);

        if (segmentIndex == 0) {
            return _value.AsSpan(firstColon + 1, secondColon - (firstColon + 1));
        }
        if (segmentIndex == 1) {
            return _value.AsSpan(secondColon + 1);
        }
        return [];
    }

    private static void ValidateNid(string nid) {
        foreach (char c in nid) {
            if (!IsAlphaNumericOrHyphen(c)) {
                throw new ArgumentException($"Invalid character '{c}' in Namespace Identifier. Only alphanumeric and hyphens allowed.", nameof(nid));
            }
        }
    }

    private static bool IsAlphaNumericOrHyphen(char c) {
        return (c >= 'a' && c <= 'z') ||
               (c >= 'A' && c <= 'Z') ||
               (c >= '0' && c <= '9') ||
               c == '-';
    }

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if (_value is null) {
            charsWritten = 0;
            return false;
        }

        if (destination.Length < _value.Length) {
            charsWritten = 0;
            return false;
        }

        _value.CopyTo(destination);
        charsWritten = _value.Length;
        return true;
    }

    public static implicit operator string(Urn urn) => urn.Value;

    #endregion
}

public class UrnJsonConverter : JsonConverter<Urn> {
    public override Urn Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? s = reader.GetString();
        return s is null ? Urn.Empty : Urn.Parse(s);
    }

    public override void Write(Utf8JsonWriter writer, Urn value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}