using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Cryptography;

/// <summary>
/// Represents an immutable, RFC 7468 PEM ("Privacy-Enhanced Mail") textually-encoded payload,
/// such as a public key, X.509 certificate, or certificate signing request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Public Data Only:</b> This type wraps a managed <see cref="string"/> and therefore cannot be
/// stored in GC-immune memory. It must never hold private key material — private key PEM text is
/// represented as <see cref="Secret{Char}"/> instead, produced via the internal <c>PemCodec</c> helper.
/// </para>
/// <para>
/// <b>Validation:</b> <see cref="Parse(string)"/> and <see cref="Create"/> both validate the PEM
/// structure (RFC 7468 pre/post-encapsulation boundaries, Base64 payload) via <see cref="PemEncoding"/>.
/// A malformed block throws <see cref="FormatException"/> rather than silently producing garbage.
/// </para>
/// <para>
/// <b>Comparison:</b> Equality and ordering compare the full PEM text (including the label boundary)
/// using ordinal semantics. Two blocks with identical DER payloads but different labels are NOT equal.
/// </para>
/// <para>
/// <b>Log Safety:</b> <see cref="ToString()"/> never returns the actual PEM content — only a sentinel
/// containing the label — to avoid accidentally dumping key material shape into logs. Use
/// <see cref="Value"/> explicitly when the raw text is genuinely needed.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(PemStringJsonConverter))]
public readonly struct PemString :
    IEquatable<PemString>,
    IComparable<PemString>,
    IComparable,
    ISpanParsable<PemString>,
    IUtf8SpanParsable<PemString>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IComparisonOperators<PemString, PemString, bool> {

    private readonly string? _value;

    /// <summary>Gets the PEM label declared in the encapsulation boundary (e.g. <see cref="PemLabel.PublicKey"/>).</summary>
    public PemLabel Label { get; }

    /// <summary>Gets the full PEM text, including the <c>-----BEGIN/END-----</c> boundaries.</summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>Gets a value indicating whether this instance is empty or uninitialized.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    /// <summary>Gets an empty <see cref="PemString"/> instance.</summary>
    public static PemString Empty => default;

    private PemString(string value, PemLabel label) {
        this._value = value;
        this.Label = label;
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes raw DER bytes into a labeled <see cref="PemString"/> block (RFC 7468).
    /// </summary>
    /// <param name="label">The PEM label to use in the encapsulation boundary (e.g. <see cref="PemLabel.PublicKey"/>).</param>
    /// <param name="der">The raw DER-encoded payload bytes.</param>
    /// <returns>A new <see cref="PemString"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="label"/> is uninitialized (empty <see cref="PemLabel.Value"/>).</exception>
    public static PemString Create(PemLabel label, ReadOnlySpan<byte> der) {
        Preca.ThrowIfNullOrWhiteSpace(label.Value);
        char[] pem = PemEncoding.Write(label.Value, der);
        return new PemString(new string(pem), label);
    }

    // ── Parsing (IParsable / ISpanParsable / IUtf8SpanParsable) ────────────────

    /// <summary>
    /// Parses PEM text into a <see cref="PemString"/>, validating its structure per RFC 7468.
    /// </summary>
    /// <param name="pem">The PEM text to parse.</param>
    /// <returns>The parsed <see cref="PemString"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pem"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="pem"/> is not a well-formed PEM block.</exception>
    public static PemString Parse(string pem) {
        Preca.ThrowIfNull(pem);
        return Parse(pem.AsSpan());
    }

    /// <summary>
    /// Parses a PEM character span into a <see cref="PemString"/>, validating its structure per RFC 7468.
    /// </summary>
    /// <param name="pem">The PEM character span to parse.</param>
    /// <returns>The parsed <see cref="PemString"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="pem"/> is not a well-formed PEM block.</exception>
    public static PemString Parse(ReadOnlySpan<char> pem) {
        PemFields fields = PemEncoding.Find(pem); // throws FormatException when malformed
        PemLabel label = PemLabel.Custom(pem[fields.Label].ToString());
        return new PemString(new string(pem), label);
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="PemString"/>.
    /// </summary>
    public static PemString Parse(ReadOnlySpan<byte> utf8Text) {
        char[] chars = new char[Encoding.UTF8.GetCharCount(utf8Text)];
        Encoding.UTF8.GetChars(utf8Text, chars);
        return Parse(chars.AsSpan());
    }

    /// <summary>
    /// Attempts to parse a PEM string into a <see cref="PemString"/> without throwing on failure.
    /// </summary>
    /// <param name="pem">The PEM text to parse.</param>
    /// <param name="result">When this method returns, contains the parsed value if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? pem, out PemString result) {
        if(string.IsNullOrWhiteSpace(pem)) {
            result = default;
            return false;
        }
        return TryParse(pem.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a PEM character span into a <see cref="PemString"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out PemString result) {
        try {
            result = Parse(s);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse a UTF-8 byte span into a <see cref="PemString"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out PemString result) {
        try {
            result = Parse(utf8Text);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static PemString IParsable<PemString>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<PemString>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PemString result) => TryParse(s, out result);
    static PemString ISpanParsable<PemString>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<PemString>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out PemString result) => TryParse(s, out result);
    static PemString IUtf8SpanParsable<PemString>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<PemString>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out PemString result) => TryParse(utf8Text, out result);

    #endregion

    // ── Label Validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates that this PEM block carries the expected label (e.g. <see cref="PemLabel.PublicKey"/>).
    /// </summary>
    /// <param name="expectedLabel">The <see cref="PemLabel"/> that this PEM block must declare.</param>
    /// <exception cref="FormatException">Thrown when the actual label does not match <paramref name="expectedLabel"/>.</exception>
    public void EnsureLabel(PemLabel expectedLabel) {
        if(this.Label != expectedLabel) {
            throw new FormatException($"Expected PEM label '{expectedLabel.Value}' but found '{this.Label.Value}'.");
        }
    }

    // ── DER Access ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes the Base64 payload of this PEM block into raw DER bytes.
    /// </summary>
    /// <returns>The decoded DER byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this instance is empty.</exception>
    public byte[] ToDerBytes() {
        Preca.ThrowIf(this.IsEmpty, static () => new InvalidOperationException("Cannot decode DER bytes from an empty PemString."));

        PemFields fields = PemEncoding.Find(this.Value);
        return Base64String.Decode(this.Value.AsSpan(fields.Base64Data));
    }

    /// <summary>
    /// Attempts to decode the Base64 payload of this PEM block directly into a destination span, without heap allocation.
    /// </summary>
    /// <param name="destination">The destination span to write DER bytes into.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if decoding succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryDecodeDer(Span<byte> destination, out int bytesWritten) {
        if(this.IsEmpty) {
            bytesWritten = 0;
            return false;
        }

        PemFields fields = PemEncoding.Find(this.Value);
        return Base64String.TryDecode(this.Value.AsSpan(fields.Base64Data), destination, out bytesWritten);
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(PemString other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is PemString other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return string.GetHashCode(this.Value, StringComparison.Ordinal);
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public int CompareTo(PemString other) {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is not PemString other) {
            throw new ArgumentException($"Object must be of type {nameof(PemString)}.", nameof(obj));
        }
        return CompareTo(other);
    }

    // ── Operators ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public static bool operator ==(PemString left, PemString right) {
        return left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator !=(PemString left, PemString right) {
        return !left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator <(PemString left, PemString right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc/>
    public static bool operator <=(PemString left, PemString right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc/>
    public static bool operator >(PemString left, PemString right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc/>
    public static bool operator >=(PemString left, PemString right) {
        return left.CompareTo(right) >= 0;
    }

    // ── Formatting (IFormattable / ISpanFormattable / IUtf8SpanFormattable) ────

    /// <summary>
    /// Returns a log-safe sentinel string (<c>"[PEM (label)]"</c>). The underlying PEM text is
    /// never returned by this method — use <see cref="Value"/> to access the raw content explicitly.
    /// </summary>
    public override string ToString() {
        return this.IsEmpty ? "[EMPTY_PEM]" : $"[PEM ({this.Label.Value})]";
    }

    /// <summary>
    /// Formats this instance. <paramref name="format"/> and <paramref name="formatProvider"/> are
    /// ignored — PEM has no culture- or format-sensitive representation.
    /// </summary>
    /// <returns>The log-safe sentinel returned by <see cref="ToString()"/>.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return ToString();
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        string text = ToString();
        if(!text.TryCopyTo(destination)) {
            charsWritten = 0;
            return false;
        }
        charsWritten = text.Length;
        return true;
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return Encoding.UTF8.TryGetBytes(ToString(), utf8Destination, out bytesWritten);
    }

    // ── Conversions ───────────────────────────────────────────────────────────

    /// <summary>Implicitly converts a <see cref="PemString"/> to its underlying PEM text.</summary>
    public static implicit operator string(PemString pem) {
        return pem.Value;
    }

    /// <summary>Implicitly converts a <see cref="PemString"/> to a <see cref="ReadOnlySpan{Char}"/> view over its PEM text.</summary>
    public static implicit operator ReadOnlySpan<char>(PemString pem) {
        return pem.Value;
    }
}