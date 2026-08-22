using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the unique identifier of a registered webhook endpoint.
/// </summary>
/// <remarks>
/// This value object exists to prevent primitive obsession: without it, a plain
/// <see cref="string"/> could accidentally be passed where an event name, a secret,
/// or some other string-typed value was expected, and the compiler would not catch the mistake.
/// <para>
/// The underlying value is intentionally unconstrained in format — callers may use a
/// database-generated <see cref="Guid"/>, a hash, a URL, or any other string representation
/// that uniquely identifies an endpoint registration within their own system.
/// </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(WebhookEndpointIdJsonConverter))]
public readonly record struct WebhookEndpointId :
    IEquatable<WebhookEndpointId>,
    IComparable<WebhookEndpointId>,
    IComparable,
    ISpanParsable<WebhookEndpointId>,
    IUtf8SpanParsable<WebhookEndpointId>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>
    /// Gets the underlying string value of the endpoint identifier, or an empty string if uninitialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Gets a default equality comparer for <see cref="WebhookEndpointId"/> using ordinal comparison with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<WebhookEndpointId> OrdinalComparer { get; } = new OrdinalEqualityComparer();

    /// <summary>
    /// Gets an equality comparer for <see cref="WebhookEndpointId"/> using case-insensitive ordinal comparison with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<WebhookEndpointId> OrdinalIgnoreCaseComparer { get; } = new OrdinalIgnoreCaseEqualityComparer();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEndpointId"/> struct.
    /// </summary>
    /// <param name="value">The identifier value. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or consists only of whitespace.
    /// </exception>
    public WebhookEndpointId(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>
    /// Parses a string into a <see cref="WebhookEndpointId"/>.
    /// </summary>
    /// <param name="s">The string value to parse.</param>
    /// <returns>A valid <see cref="WebhookEndpointId"/> instance.</returns>
    public static WebhookEndpointId Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new WebhookEndpointId(s);
    }

    /// <summary>
    /// Parses a character span into a <see cref="WebhookEndpointId"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="WebhookEndpointId"/> instance.</returns>
    public static WebhookEndpointId Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace()) {
            throw new ArgumentException("Webhook endpoint identifier cannot be empty or whitespace.", nameof(s));
        }
        return new WebhookEndpointId(s.ToString());
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into a <see cref="WebhookEndpointId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A valid <see cref="WebhookEndpointId"/> instance.</returns>
    public static WebhookEndpointId Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out WebhookEndpointId result)) {
            throw new ArgumentException("Webhook endpoint identifier cannot be empty or invalid UTF-8.", nameof(utf8Text));
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="WebhookEndpointId"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed identifier if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookEndpointId result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }

        result = new WebhookEndpointId(s);
        return true;
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="WebhookEndpointId"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed identifier if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out WebhookEndpointId result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }

        result = new WebhookEndpointId(s.ToString());
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into a <see cref="WebhookEndpointId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed identifier if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out WebhookEndpointId result) {
        if(utf8Text.IsEmpty) {
            result = default;
            return false;
        }

        try {
            string str = Encoding.UTF8.GetString(utf8Text);
            if(string.IsNullOrWhiteSpace(str)) {
                result = default;
                return false;
            }
            result = new WebhookEndpointId(str);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Formats the endpoint identifier into the specified character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if the format operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>
    /// Formats the endpoint identifier into the specified UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if the format operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <inheritdoc/>
    public int CompareTo(WebhookEndpointId other) =>
        string.CompareOrdinal(this.Value, other.Value);

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) {
            return 1;
        }
        if(obj is WebhookEndpointId other) {
            return CompareTo(other);
        }
        throw new ArgumentException($"Object must be of type {nameof(WebhookEndpointId)}.", nameof(obj));
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        this.Value;

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(utf8Destination, out bytesWritten);

    static WebhookEndpointId IParsable<WebhookEndpointId>.Parse(string s, IFormatProvider? provider) =>
        Parse(s);

    static bool IParsable<WebhookEndpointId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookEndpointId result) =>
        TryParse(s, out result);

    static WebhookEndpointId ISpanParsable<WebhookEndpointId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        Parse(s);

    static bool ISpanParsable<WebhookEndpointId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookEndpointId result) =>
        TryParse(s, out result);

    static WebhookEndpointId IUtf8SpanParsable<WebhookEndpointId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(utf8Text);

    static bool IUtf8SpanParsable<WebhookEndpointId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookEndpointId result) =>
        TryParse(utf8Text, out result);

    private sealed class OrdinalEqualityComparer :
        IEqualityComparer<WebhookEndpointId>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookEndpointId> {
        public bool Equals(WebhookEndpointId x, WebhookEndpointId y) =>
            string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(WebhookEndpointId obj) =>
            StringComparer.Ordinal.GetHashCode(obj.Value);

        public bool Equals(ReadOnlySpan<char> alternate, WebhookEndpointId other) =>
            alternate.Equals(other.Value.AsSpan(), StringComparison.Ordinal);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.Ordinal);

        public WebhookEndpointId Create(ReadOnlySpan<char> alternate) =>
            WebhookEndpointId.Parse(alternate);
    }

    private sealed class OrdinalIgnoreCaseEqualityComparer :
        IEqualityComparer<WebhookEndpointId>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookEndpointId> {
        public bool Equals(WebhookEndpointId x, WebhookEndpointId y) =>
            string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(WebhookEndpointId obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);

        public bool Equals(ReadOnlySpan<char> alternate, WebhookEndpointId other) =>
            alternate.Equals(other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public WebhookEndpointId Create(ReadOnlySpan<char> alternate) =>
            WebhookEndpointId.Parse(alternate);
    }
}

/// <summary>
/// Custom JSON converter for <see cref="WebhookEndpointId"/> ensuring direct string serialization and dictionary key support.
/// </summary>
public sealed class WebhookEndpointIdJsonConverter : JsonConverter<WebhookEndpointId> {
    /// <inheritdoc/>
    public override WebhookEndpointId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? value = reader.GetString();
        return value is null ? default : WebhookEndpointId.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WebhookEndpointId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override WebhookEndpointId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return WebhookEndpointId.Parse(reader.GetString()!);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, WebhookEndpointId value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}