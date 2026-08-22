using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the unique, strongly-typed identifier of a webhook delivery job.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(WebhookJobIdJsonConverter))]
public readonly record struct WebhookJobId :
    IEquatable<WebhookJobId>,
    IComparable<WebhookJobId>,
    IComparable,
    ISpanParsable<WebhookJobId>,
    IUtf8SpanParsable<WebhookJobId>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>
    /// Gets the underlying string value of the job identifier, or an empty string if uninitialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Gets a default equality comparer for <see cref="WebhookJobId"/> using ordinal comparison with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<WebhookJobId> OrdinalComparer { get; } = new OrdinalEqualityComparer();

    /// <summary>
    /// Gets an equality comparer for <see cref="WebhookJobId"/> using case-insensitive ordinal comparison with zero-allocation span lookup support.
    /// </summary>
    public static IEqualityComparer<WebhookJobId> OrdinalIgnoreCaseComparer { get; } = new OrdinalIgnoreCaseEqualityComparer();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookJobId"/> struct with the specified value.
    /// </summary>
    /// <param name="value">The identifier value. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    public WebhookJobId(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>
    /// Creates a new time-ordered unique <see cref="WebhookJobId"/> using Version 7 UUID.
    /// </summary>
    /// <returns>A new <see cref="WebhookJobId"/> instance.</returns>
    public static WebhookJobId NewJobId() => new($"job_{Guid.CreateVersion7():N}");

    /// <summary>
    /// Parses a string into a <see cref="WebhookJobId"/>.
    /// </summary>
    /// <param name="s">The string value to parse.</param>
    /// <returns>A valid <see cref="WebhookJobId"/> instance.</returns>
    public static WebhookJobId Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new WebhookJobId(s);
    }

    /// <summary>
    /// Parses a character span into a <see cref="WebhookJobId"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A valid <see cref="WebhookJobId"/> instance.</returns>
    public static WebhookJobId Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace()) {
            throw new ArgumentException("Webhook job identifier cannot be empty or whitespace.", nameof(s));
        }
        return new WebhookJobId(s.ToString());
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into a <see cref="WebhookJobId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A valid <see cref="WebhookJobId"/> instance.</returns>
    public static WebhookJobId Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out WebhookJobId result)) {
            throw new ArgumentException("Webhook job identifier cannot be empty or invalid UTF-8.", nameof(utf8Text));
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="WebhookJobId"/>.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed identifier if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookJobId result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }

        result = new WebhookJobId(s);
        return true;
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="WebhookJobId"/>.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed identifier if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out WebhookJobId result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }

        result = new WebhookJobId(s.ToString());
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into a <see cref="WebhookJobId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed identifier if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out WebhookJobId result) {
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
            result = new WebhookJobId(str);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Formats the job identifier into the specified character span.
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
    /// Formats the job identifier into the specified UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if the format operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done) {
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <inheritdoc/>
    public int CompareTo(WebhookJobId other) =>
        string.CompareOrdinal(this.Value, other.Value);

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) {
            return 1;
        }
        if(obj is WebhookJobId other) {
            return CompareTo(other);
        }
        throw new ArgumentException($"Object must be of type {nameof(WebhookJobId)}.", nameof(obj));
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        this.Value;

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        TryFormat(utf8Destination, out bytesWritten);

    static WebhookJobId IParsable<WebhookJobId>.Parse(string s, IFormatProvider? provider) =>
        Parse(s);

    static bool IParsable<WebhookJobId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookJobId result) =>
        TryParse(s, out result);

    static WebhookJobId ISpanParsable<WebhookJobId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        Parse(s);

    static bool ISpanParsable<WebhookJobId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookJobId result) =>
        TryParse(s, out result);

    static WebhookJobId IUtf8SpanParsable<WebhookJobId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) =>
        Parse(utf8Text);

    static bool IUtf8SpanParsable<WebhookJobId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookJobId result) =>
        TryParse(utf8Text, out result);

    private sealed class OrdinalEqualityComparer :
        IEqualityComparer<WebhookJobId>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookJobId> {
        public bool Equals(WebhookJobId x, WebhookJobId y) =>
            string.Equals(x.Value, y.Value, StringComparison.Ordinal);

        public int GetHashCode(WebhookJobId obj) =>
            StringComparer.Ordinal.GetHashCode(obj.Value);

        public bool Equals(ReadOnlySpan<char> alternate, WebhookJobId other) =>
            alternate.Equals(other.Value.AsSpan(), StringComparison.Ordinal);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.Ordinal);

        public WebhookJobId Create(ReadOnlySpan<char> alternate) =>
            WebhookJobId.Parse(alternate);
    }

    private sealed class OrdinalIgnoreCaseEqualityComparer :
        IEqualityComparer<WebhookJobId>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookJobId> {
        public bool Equals(WebhookJobId x, WebhookJobId y) =>
            string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(WebhookJobId obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);

        public bool Equals(ReadOnlySpan<char> alternate, WebhookJobId other) =>
            alternate.Equals(other.Value.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ReadOnlySpan<char> alternate) =>
            string.GetHashCode(alternate, StringComparison.OrdinalIgnoreCase);

        public WebhookJobId Create(ReadOnlySpan<char> alternate) =>
            WebhookJobId.Parse(alternate);
    }
}

/// <summary>
/// Custom JSON converter for <see cref="WebhookJobId"/> ensuring direct string serialization and dictionary key support.
/// </summary>
public sealed class WebhookJobIdJsonConverter : JsonConverter<WebhookJobId> {
    /// <inheritdoc/>
    public override WebhookJobId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? value = reader.GetString();
        return value is null ? default : WebhookJobId.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WebhookJobId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override WebhookJobId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return WebhookJobId.Parse(reader.GetString()!);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, WebhookJobId value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Value);
    }
}
