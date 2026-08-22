using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
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
    IComparable<Urn>,
    IComparable,
    IParsable<Urn>,
    ISpanParsable<Urn>,
    IUtf8SpanParsable<Urn>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<Urn, Urn, bool> {

    private const string Prefix = "urn";
    private const char Separator = ':';
    private const int PrefixLength = 4; // "urn:" length

    // Storing the full value is safer and faster for a ValueObject than storing parts.
    private readonly string _value;
    private readonly ushort _nidEnd;
    private readonly ushort _nssStart;
    /// <summary>
    /// Gets the Namespace Identifier (NID). 
    /// <para>Example: "user" in "urn:user:123".</para>
    /// </summary>
    public ReadOnlySpan<char> Namespace => string.IsNullOrEmpty(this._value) ? [] : this._value.AsSpan(PrefixLength, this._nidEnd - PrefixLength);

    /// <summary>
    /// Gets the Namespace Specific String (NSS). 
    /// <para>Example: "123" in "urn:user:123".</para>
    /// </summary>
    public ReadOnlySpan<char> Identity => string.IsNullOrEmpty(this._value) ? [] : this._value.AsSpan(this._nssStart);

    /// <summary>
    /// Represents an empty URN.
    /// </summary>
    public static Urn Empty => default;

    /// <summary>
    /// Gets the full URN string value.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    private Urn(string value) {
        this._value = value;

        if(string.IsNullOrEmpty(value)) {
            this._nidEnd = 0;
            this._nssStart = 0;
            return;
        }

        int firstColon = 3;
        int secondColon = value.IndexOf(Separator, firstColon + 1);

        this._nidEnd = (ushort)secondColon;
        this._nssStart = (ushort)(secondColon + 1);
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
            (string? n, Guid g) = state;

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
    /// Uses <see cref="string.Create{TState}(int, TState, System.Buffers.SpanAction{char, TState})"/> to minimize allocations.
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
            (string? id, string[]? segs) = state;

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
                ReadOnlySpan<char> currentSeg = segs[i].AsSpan();
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

    /// <summary>
    /// Creates a URN from a namespace and a cryptographic hash (Md5, Sha256, etc.).
    /// Uses high-performance interpolation to avoid temporary string allocations.
    /// </summary>
    /// <param name="nid">The Namespace Identifier (e.g., "sha256").</param>
    /// <param name="hash">The hash instance.</param>
    /// <returns>A new <see cref="Urn"/> instance formatted in lowercase hex.</returns>
    public static Urn Create<THash>(string nid, THash hash) where THash : struct, ISpanFormattable {
        Preca.ThrowIfNullOrWhiteSpace(nid);
        ValidateNid(nid);

        // .NET InterpolatedStringHandler, 'hash:x' formatını algılayıp 
        // ISpanFormattable arayüzü üzerinden doğrudan hedef span'e yazar. Allocation = 0.
        return new Urn($"{Prefix}{Separator}{nid}{Separator}{hash:x}");
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Parses a string into a <see cref="Urn"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The parsed Urn.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown if the format is invalid.</exception>
    public static Urn Parse(string s) {
        Preca.ThrowIfNull(s);
        return TryParse(s.AsSpan(), out Urn result) ? result : throw new FormatException($"Invalid URN format: '{s}'. Expected 'urn:<nid>:<nss>'.");
    }

    /// <summary>
    /// Parses a ReadOnlySpan into a <see cref="Urn"/>.
    /// </summary>
    public static Urn Parse(ReadOnlySpan<char> s) {
        return TryParse(s, out Urn result) ? result : throw new FormatException($"Invalid URN format: '{s}'.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Urn"/>.
    /// </summary>
    public static Urn Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out Urn result)) {
            return result;
        }
        throw new FormatException("Invalid UTF-8 sequence for Urn.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="Urn"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Urn result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a ReadOnlySpan into a <see cref="Urn"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Urn result) {
        if(s.Length < 7 || !s.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)) {
            result = default; return false;
        }

        int secondColon = s[PrefixLength..].IndexOf(Separator);
        if(secondColon < 1) {
            result = default; return false;
        }

        secondColon += PrefixLength;
        if(secondColon >= s.Length - 1) {
            result = default; return false;
        }

        ReadOnlySpan<char> nid = s[PrefixLength..secondColon];
        foreach(char c in nid) {
            if(!IsAlphaNumericOrHyphen(c)) {
                result = default; return false;
            }
        }

        result = new Urn(s.ToString());
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="Urn"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Urn result) {
        if(utf8Text.IsEmpty) { result = default; return false; }
        Span<char> chars = stackalloc char[utf8Text.Length <= 128 ? utf8Text.Length : 128];
        char[]? rented = utf8Text.Length > 128 ? System.Buffers.ArrayPool<char>.Shared.Rent(utf8Text.Length) : null;
        Span<char> buf = rented is not null ? rented.AsSpan(0, utf8Text.Length) : chars;
        try {
            if(System.Text.Encoding.UTF8.GetChars(utf8Text, buf) == utf8Text.Length) {
                return TryParse(buf, out result);
            }
            result = default;
            return false;
        }
        finally {
            if(rented is not null) System.Buffers.ArrayPool<char>.Shared.Return(rented);
        }
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Urn IParsable<Urn>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Urn>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Urn result) => TryParse(s, out result);
    static Urn ISpanParsable<Urn>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Urn>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Urn result) => TryParse(s, out result);
    static Urn IUtf8SpanParsable<Urn>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Urn>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Urn result) => TryParse(utf8Text, out result);

    #endregion

    #region Helpers & Formatting

    private static void ValidateNid(string nid) {
        foreach(char c in nid) {
            if(!IsAlphaNumericOrHyphen(c))
                throw new ArgumentException($"Invalid character '{c}' in Namespace Identifier. Only alphanumeric and hyphens allowed.", nameof(nid));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAlphaNumericOrHyphen(char c) {
        return c is >= 'a' and <= 'z' or
               >= 'A' and <= 'Z' or
               >= '0' and <= '9' or
               '-';
    }

    /// <summary>
    /// Returns the string representation of the URN.
    /// </summary>
    public override string ToString() => this.Value;

    /// <summary>
    /// Returns the string representation using the specified format.
    /// </summary>
    public string ToString(string? format) => this.Value;

    /// <summary>
    /// Returns the string representation using the specified format and format provider.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

    /// <summary>
    /// Tries to format the value of the current instance into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the value of the current instance into the destination character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the value of the current instance into the provided span of characters.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this._value is null) {
            charsWritten = 0;
            return false;
        }

        if(destination.Length < this._value.Length) {
            charsWritten = 0;
            return false;
        }

        this._value.CopyTo(destination);
        charsWritten = this._value.Length;
        return true;
    }

    /// <summary>
    /// Tries to format the value into the destination UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the value into the destination UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the value into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = System.Text.Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Destination);
        return true;
    }

    /// <summary>
    /// Implicitly converts a <see cref="Urn"/> to a <see cref="string"/>.
    /// </summary>
    public static implicit operator string(Urn urn) => urn.Value;

    /// <inheritdoc/>
    public bool Equals(Urn other) => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Value.GetHashCode(StringComparison.Ordinal);

    #endregion

    #region Comparison & Operators

    /// <inheritdoc/>
    public int CompareTo(Urn other) => string.Compare(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Urn other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Urn)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Urn left, Urn right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Urn left, Urn right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Urn left, Urn right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Urn left, Urn right) => left.CompareTo(right) <= 0;

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Urn"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Urn> OrdinalComparer => UrnOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Urn"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Urn> OrdinalIgnoreCaseComparer => UrnOrdinalIgnoreCaseComparer.Instance;

    private sealed class UrnOrdinalComparer : IEqualityComparer<Urn>, IAlternateEqualityComparer<ReadOnlySpan<char>, Urn> {
        public static UrnOrdinalComparer Instance { get; } = new();

        public bool Equals(Urn x, Urn y) => string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(Urn obj) => obj.Value.GetHashCode(StringComparison.Ordinal);

        public bool Equals(ReadOnlySpan<char> alternate, Urn other) => alternate.SequenceEqual(other.Value.AsSpan());

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.Ordinal);

        public Urn Create(ReadOnlySpan<char> alternate) => Urn.Parse(alternate);
    }

    private sealed class UrnOrdinalIgnoreCaseComparer : IEqualityComparer<Urn>, IAlternateEqualityComparer<ReadOnlySpan<char>, Urn> {
        public static UrnOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Urn x, Urn y) => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Urn obj) => string.GetHashCode(obj.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public bool Equals(ReadOnlySpan<char> alternate, Urn other) => alternate.Equals(other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) => string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public Urn Create(ReadOnlySpan<char> alternate) => Urn.Parse(alternate);
    }

    #endregion
}