using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a structured cryptographic webhook signature containing timestamp, scheme, and hash value.
/// </summary>
/// <remarks>
/// The signature header follows the standard format:
/// <c>t={timestamp},{scheme}={signature}</c> (e.g., <c>t=1724190000,v1=4f53cda1...</c>).
/// </remarks>
[DebuggerDisplay("{HeaderValue,nq}")]
[JsonConverter(typeof(WebhookSignatureJsonConverter))]
public readonly record struct WebhookSignature :
    IEquatable<WebhookSignature>,
    IComparable<WebhookSignature>,
    IComparable,
    ISpanParsable<WebhookSignature>,
    IUtf8SpanParsable<WebhookSignature>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _scheme;
    private readonly string _signature;
    private readonly string _headerValue;

    /// <summary>
    /// Gets the Unix timestamp when the signature was generated.
    /// </summary>
    public UnixTimestamp Timestamp { get; }

    /// <summary>
    /// Gets the scheme prefix associated with the cryptographic algorithm (e.g., "v1" for HMAC-SHA256, "v2" for HMAC-SHA512).
    /// </summary>
    public string Scheme => this._scheme ?? string.Empty;

    /// <summary>
    /// Gets the lowercase hexadecimal signature hash.
    /// </summary>
    public string Signature => this._signature ?? string.Empty;

    /// <summary>
    /// Gets the canonical HTTP header value (e.g., "t=1724190000,v1=4f53c...").
    /// </summary>
    public string HeaderValue => this._headerValue ?? string.Empty;

    /// <summary>
    /// Gets a default equality comparer for <see cref="WebhookSignature"/> with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<WebhookSignature> Comparer { get; } = new SignatureEqualityComparer();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSignature"/> struct.
    /// </summary>
    /// <param name="timestamp">The Unix timestamp when the signature was produced.</param>
    /// <param name="scheme">The scheme prefix. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    /// <param name="signature">The raw signature string. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    public WebhookSignature(UnixTimestamp timestamp, string scheme, string signature) {
        Preca.ThrowIfNullOrWhiteSpace(scheme);
        Preca.ThrowIfNullOrWhiteSpace(signature);

        this.Timestamp = timestamp;
        this._scheme = scheme;
        this._signature = signature;
        this._headerValue = $"t={timestamp.TotalSeconds},{scheme}={signature}";
    }

    /// <summary>
    /// Parses a canonical signature header string into a <see cref="WebhookSignature"/>.
    /// </summary>
    /// <param name="s">The header string in format <c>t={timestamp},{scheme}={signature}</c>.</param>
    /// <returns>A valid <see cref="WebhookSignature"/> instance.</returns>
    public static WebhookSignature Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        if(!TryParse(s.AsSpan(), out WebhookSignature result)) {
            throw new FormatException($"String '{s}' was not recognized as a valid canonical WebhookSignature header.");
        }
        return result;
    }

    /// <summary>
    /// Parses a character span into a <see cref="WebhookSignature"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="WebhookSignature"/> instance.</returns>
    public static WebhookSignature Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out WebhookSignature result)) {
            throw new FormatException("Span was not recognized as a valid canonical WebhookSignature header.");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into a <see cref="WebhookSignature"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A valid <see cref="WebhookSignature"/> instance.</returns>
    public static WebhookSignature Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out WebhookSignature result)) {
            throw new FormatException("UTF-8 byte span was not recognized as a valid canonical WebhookSignature header.");
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a signature header string into a <see cref="WebhookSignature"/>.
    /// </summary>
    /// <param name="s">The header string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed signature if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookSignature result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="WebhookSignature"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed signature if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out WebhookSignature result) {
        result = default;
        ReadOnlySpan<char> span = s.Trim();
        if(span.IsEmpty) {
            return false;
        }

        if(!span.StartsWith("t=", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        int commaIndex = span.IndexOf(',');
        if(commaIndex < 0) {
            return false;
        }

        ReadOnlySpan<char> timeSpan = span[2..commaIndex].Trim();
        if(!long.TryParse(timeSpan, out long totalSeconds)) {
            return false;
        }

        ReadOnlySpan<char> remainder = span[(commaIndex + 1)..].Trim();
        int equalsIndex = remainder.IndexOf('=');
        if(equalsIndex <= 0 || equalsIndex >= remainder.Length - 1) {
            return false;
        }

        ReadOnlySpan<char> schemeSpan = remainder[..equalsIndex].Trim();
        ReadOnlySpan<char> sigSpan = remainder[(equalsIndex + 1)..].Trim();

        if(schemeSpan.IsEmpty || sigSpan.IsEmpty) {
            return false;
        }

        result = new WebhookSignature(
            UnixTimestamp.FromSeconds(totalSeconds),
            schemeSpan.ToString(),
            sigSpan.ToString());

        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into a <see cref="WebhookSignature"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed signature if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out WebhookSignature result) {
        if(utf8Text.IsEmpty) {
            result = default;
            return false;
        }

        try {
            string str = Encoding.UTF8.GetString(utf8Text);
            return TryParse(str.AsSpan(), out result);
        }
        catch {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Formats the signature header into the specified character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if the format operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.HeaderValue.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.HeaderValue.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>
    /// Formats the signature header into the specified UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if the format operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return Utf8.FromUtf16(this.HeaderValue.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    /// <inheritdoc/>
    public override string ToString() => this.HeaderValue;

    /// <inheritdoc/>
    public int CompareTo(WebhookSignature other) {
        int timestampComparison = this.Timestamp.CompareTo(other.Timestamp);
        if(timestampComparison != 0) {
            return timestampComparison;
        }

        int schemeComparison = string.CompareOrdinal(this.Scheme, other.Scheme);
        if(schemeComparison != 0) {
            return schemeComparison;
        }

        return string.CompareOrdinal(this.Signature, other.Signature);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) {
            return 1;
        }
        if(obj is WebhookSignature other) {
            return CompareTo(other);
        }
        throw new ArgumentException($"Object must be of type {nameof(WebhookSignature)}.", nameof(obj));
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        this.HeaderValue;

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(utf8Destination, out bytesWritten);

    static WebhookSignature IParsable<WebhookSignature>.Parse(string s, IFormatProvider? provider) =>
        Parse(s);

    static bool IParsable<WebhookSignature>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookSignature result) =>
        TryParse(s, out result);

    static WebhookSignature ISpanParsable<WebhookSignature>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        Parse(s);

    static bool ISpanParsable<WebhookSignature>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookSignature result) =>
        TryParse(s, out result);

    static WebhookSignature IUtf8SpanParsable<WebhookSignature>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(utf8Text);

    static bool IUtf8SpanParsable<WebhookSignature>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookSignature result) =>
        TryParse(utf8Text, out result);

    private sealed class SignatureEqualityComparer :
        IEqualityComparer<WebhookSignature>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookSignature> {
        public bool Equals(WebhookSignature x, WebhookSignature y) =>
            string.Equals(x.HeaderValue, y.HeaderValue, StringComparison.Ordinal);

        public int GetHashCode(WebhookSignature obj) =>
            StringComparer.Ordinal.GetHashCode(obj.HeaderValue);

        public bool Equals(ReadOnlySpan<char> alternate, WebhookSignature other) =>
            alternate.Equals(other.HeaderValue.AsSpan(), StringComparison.Ordinal);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.Ordinal);

        public WebhookSignature Create(ReadOnlySpan<char> alternate) =>
            WebhookSignature.Parse(alternate);
    }
}

/// <summary>
/// Custom JSON converter for <see cref="WebhookSignature"/> serializing directly to and from its canonical header string.
/// </summary>
public sealed class WebhookSignatureJsonConverter : JsonConverter<WebhookSignature> {
    /// <inheritdoc/>
    public override WebhookSignature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? value = reader.GetString();
        return value is null ? default : WebhookSignature.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WebhookSignature value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.HeaderValue);
    }
}
