using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Wiaoj.Preconditions; // Preca sınıfının olduğu namespace
using Wiaoj.Primitives.JsonConverters;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents a Uniform Resource Name (URN) conforming to RFC 8141.
/// <para>
/// Structure: <c>urn:&lt;nid&gt;:&lt;nss&gt;</c><br/>
/// Example: <c>urn:isbn:978-0-123-45678-9</c> or <c>urn:user:123456789</c>
/// </para>
/// <para>
/// This struct is immutable and optimized for high-performance scenarios using Span operations.
/// </para>
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(UrnJsonConverter))]
public readonly record struct Urn :
    IEquatable<Urn>,
    ISpanParsable<Urn>,
    ISpanFormattable {

    private const string Prefix = "urn";
    private const char Separator = ':';
    private const int PrefixLength = 4; // "urn:" length

    // Storing the full value is safer and faster for a ValueObject than storing parts.
    private readonly string _value;

    /// <summary>
    /// Gets the Namespace Identifier (NID). 
    /// <para>Example: "user" in "urn:user:123".</para>
    /// </summary>
    public ReadOnlySpan<char> Namespace => ParseSegment(0);

    /// <summary>
    /// Gets the Namespace Specific String (NSS). 
    /// <para>Example: "123" in "urn:user:123".</para>
    /// </summary>
    public ReadOnlySpan<char> Identity => ParseSegment(1);

    /// <summary>
    /// Represents an empty URN.
    /// </summary>
    public static Urn Empty => default;

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
        nid = Namespace;
        nss = Identity;
    }

    #region Factory Methods (Zero-Allocation & Optimized)

    /// <summary>
    /// Creates a URN from a namespace and a string identifier.
    /// </summary>
    /// <param name="nid">The Namespace Identifier (e.g., "user"). Must be alphanumeric/hyphen.</param>
    /// <param name="nss">The Namespace Specific String (e.g., "12345").</param>
    /// <returns>A new <see cref="Urn"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if NID contains invalid characters or is empty.</exception>
    public static Urn Create(string nid, string nss) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        Preca.ThrowIfNullOrWhiteSpace(nss);
        ValidateNid(nid);

        // Modern .NET Interpolation is highly optimized via DefaultInterpolatedStringHandler
        return new Urn($"{Prefix}{Separator}{nid}{Separator}{nss}");
    }

    /// <summary>
    /// Creates a URN from a namespace and a <see cref="Guid"/>.
    /// Optimized to write the Guid directly into the string buffer without intermediate allocations.
    /// </summary>
    /// <param name="nid">The Namespace Identifier.</param>
    /// <param name="id">The Guid identifier.</param>
    /// <returns>A new <see cref="Urn"/> instance (e.g., "urn:session:550e8400...").</returns>
    public static Urn Create(string nid, Guid id) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        ValidateNid(nid);

        // Calculation:
        // "urn:" (4) + nid.Length + ":" (1) + Guid (36)
        // Standard Guid format 'D' is 36 chars (hyphenated)
        int length = PrefixLength + nid.Length + 1 + 36;

        string urnString = string.Create(length, (nid, id), (span, state) => {
            var (n, g) = state;

            // 1. Write "urn:"
            "urn:".AsSpan().CopyTo(span);
            span = span[PrefixLength..];

            // 2. Write NID
            n.AsSpan().CopyTo(span);
            span = span[n.Length..];

            // 3. Write Separator
            span[0] = Separator;
            span = span[1..];

            // 4. Write Guid DIRECTLY (No intermediate .ToString() string allocation)
            bool success = g.TryFormat(span, out _, "D");
            Debug.Assert(success, "Guid TryFormat failed in calculated buffer.");
        });

        return new Urn(urnString);
    }

    /// <summary>
    /// Creates a URN from a namespace and a <see cref="SnowflakeId"/>.
    /// Uses high-performance interpolation to avoid temporary strings.
    /// </summary>
    /// <param name="nid">The Namespace Identifier.</param>
    /// <param name="id">The Snowflake identifier.</param>
    /// <returns>A new <see cref="Urn"/> instance.</returns>
    public static Urn Create(string nid, SnowflakeId id) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        ValidateNid(nid);

        // NOTE: In .NET 6+, string interpolation ($"...") uses ISpanFormattable internally.
        // It writes directly to a stack buffer, avoiding the id.ToString() allocation.
        // This acts exactly like the manual string.Create optimization above but is cleaner for variable lengths.
        return new Urn($"{Prefix}{Separator}{nid}{Separator}{id}");
    }

    /// <summary>
    /// Creates a hierarchical URN from a namespace and two segments.
    /// </summary>
    /// <example>Urn.Create("order", "2024", "10") -> "urn:order:2024:10"</example>
    public static Urn Create(string nid, string segment1, string segment2) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        ValidateNid(nid);
        Preca.ThrowIfNullOrWhiteSpace(segment1);
        Preca.ThrowIfNullOrWhiteSpace(segment2);

        // Ensure segments don't break the structure (though URNs allow colons in NSS, 
        // hierarchical creation implies they are separators).
        Preca.ThrowIfContains(segment1, Separator);
        Preca.ThrowIfContains(segment2, Separator);

        return new Urn($"{Prefix}{Separator}{nid}{Separator}{segment1}{Separator}{segment2}");
    }

    /// <summary>
    /// Creates a hierarchical URN from a namespace and three segments.
    /// </summary>
    public static Urn Create(string nid, string segment1, string segment2, string segment3) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        ValidateNid(nid);
        Preca.ThrowIfNullOrWhiteSpace(segment1);
        Preca.ThrowIfNullOrWhiteSpace(segment2);
        Preca.ThrowIfNullOrWhiteSpace(segment3);

        Preca.ThrowIfContains(segment1, Separator);
        Preca.ThrowIfContains(segment2, Separator);
        Preca.ThrowIfContains(segment3, Separator);

        return new Urn($"{Prefix}{Separator}{nid}{Separator}{segment1}{Separator}{segment2}{Separator}{segment3}");
    }

    /// <summary>
    /// Creates a URN from a namespace and multiple segments.
    /// Uses <see cref="string.Create"/> to minimize allocations.
    /// </summary>
    /// <param name="nid">The Namespace Identifier.</param>
    /// <param name="segments">An array of segments to join.</param>
    /// <returns>A new <see cref="Urn"/> instance.</returns>
    public static Urn Create(string nid, params string[] segments) {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        ValidateNid(nid);

        if(segments is null || segments.Length == 0)
            throw new ArgumentException("At least one segment is required.", nameof(segments));

        // -- Phase 1: Calculate Total Length & Validate --
        int totalLength = PrefixLength + nid.Length + 1; // "urn:" + nid + ":"

        for(int i = 0; i < segments.Length; i++) {
            string seg = segments[i];
            Preca.ThrowIfNullOrWhiteSpace(seg);
            Preca.ThrowIfContains(seg, Separator);

            totalLength += seg.Length;
            if(i < segments.Length - 1) {
                totalLength += 1; // Add separator
            }
        }

        // -- Phase 2: Direct Write (Single Allocation) --
        string urnString = string.Create(totalLength, (nid, segments), (span, state) => {
            var (id, segs) = state;

            // Write "urn:"
            "urn:".AsSpan().CopyTo(span);
            span = span[4..];

            // Write NID
            id.AsSpan().CopyTo(span);
            span = span[id.Length..];

            // Write Separator
            span[0] = Separator;
            span = span[1..];

            // Write Segments
            for(int i = 0; i < segs.Length; i++) {
                var currentSeg = segs[i].AsSpan();
                currentSeg.CopyTo(span);
                span = span[currentSeg.Length..];

                if(i < segs.Length - 1) {
                    span[0] = Separator;
                    span = span[1..];
                }
            }
        });

        return new Urn(urnString);
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="Urn"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="provider">Format provider (optional).</param>
    /// <returns>The parsed Urn.</returns>
    /// <exception cref="FormatException">Thrown if the format is invalid.</exception>
    public static Urn Parse(string s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var result) ? result : throw new FormatException($"Invalid URN format: '{s}'. Expected 'urn:<nid>:<nss>'.");

    /// <summary>
    /// Tries to parse a string into a <see cref="Urn"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Urn result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// Parses a ReadOnlySpan into a <see cref="Urn"/>.
    /// </summary>
    public static Urn Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null) =>
        TryParse(s, provider, out var result) ? result : throw new FormatException($"Invalid URN format: '{s}'.");

    /// <summary>
    /// Tries to parse a ReadOnlySpan into a <see cref="Urn"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Urn result) {
        // Validation Rules:
        // 1. Must start with "urn:" (Case-insensitive)
        // 2. Must have at least one NID char
        // 3. Must have at least one NSS char

        if(s.Length < 7 || !s.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)) {
            result = default; return false;
        }

        // Find separator after "urn:"
        int firstColon = 3;
        int secondColon = s[4..].IndexOf(Separator);

        // NID empty check
        if(secondColon < 1) {
            result = default; return false;
        }

        secondColon += 4; // Adjust index to absolute position

        // NSS empty check
        if(secondColon >= s.Length - 1) {
            result = default; return false;
        }

        // Validate NID Chars (Allocation-free check)
        ReadOnlySpan<char> nid = s[4..secondColon];
        foreach(char c in nid) {
            if(!IsAlphaNumericOrHyphen(c)) {
                result = default; return false;
            }
        }

        result = new Urn(s.ToString());
        return true;
    }

    #endregion

    #region Helpers & Formatting

    // Internal helper to get span slices without allocation
    private ReadOnlySpan<char> ParseSegment(int segmentIndex) {
        if(string.IsNullOrEmpty(_value)) return [];

        int firstColon = 3;
        int secondColon = _value.IndexOf(Separator, firstColon + 1);

        // Should catch invalid internal state, though factory prevents it.
        if(secondColon == -1) return [];

        if(segmentIndex == 0) // NID
            return _value.AsSpan(firstColon + 1, secondColon - (firstColon + 1));

        if(segmentIndex == 1) // NSS (Rest of the string)
            return _value.AsSpan(secondColon + 1);

        return [];
    }

    private static void ValidateNid(string nid) {
        foreach(char c in nid) {
            if(!IsAlphaNumericOrHyphen(c))
                throw new ArgumentException($"Invalid character '{c}' in Namespace Identifier. Only alphanumeric and hyphens allowed.", nameof(nid));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAlphaNumericOrHyphen(char c) {
        return (c >= 'a' && c <= 'z') ||
               (c >= 'A' && c <= 'Z') ||
               (c >= '0' && c <= '9') ||
               c == '-';
    }

    /// <summary>
    /// Returns the string representation of the URN.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Returns the string representation using the specified format.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    /// <summary>
    /// Tries to format the value of the current instance into the provided span of characters.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(_value is null) {
            charsWritten = 0;
            return false;
        }

        if(destination.Length < _value.Length) {
            charsWritten = 0;
            return false;
        }

        _value.CopyTo(destination);
        charsWritten = _value.Length;
        return true;
    }

    /// <summary>
    /// Implicitly converts a <see cref="Urn"/> to a <see cref="string"/>.
    /// </summary>
    public static implicit operator string(Urn urn) => urn.Value;

    /// <inheritdoc/>
    public bool Equals(Urn other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
     
    /// <inheritdoc/>
    override public int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    #endregion
}