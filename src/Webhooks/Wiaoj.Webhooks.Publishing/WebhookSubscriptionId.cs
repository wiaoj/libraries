using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents the unique, strongly-typed identifier of a webhook event subscription.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(WebhookSubscriptionIdJsonConverter))]
public readonly record struct WebhookSubscriptionId :
    IEquatable<WebhookSubscriptionId>,
    IComparable<WebhookSubscriptionId>,
    IComparable,
    ISpanParsable<WebhookSubscriptionId>,
    IUtf8SpanParsable<WebhookSubscriptionId>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>Gets the raw string value of the subscription identifier.</summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>Initializes a new instance with the specified string value.</summary>
    public WebhookSubscriptionId(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>Creates a new time-ordered unique subscription identifier (UUIDv7).</summary>
    public static WebhookSubscriptionId NewId() => new($"sub_{Guid.CreateVersion7():N}");

    /// <summary>Parses a string into a <see cref="WebhookSubscriptionId"/>.</summary>
    public static WebhookSubscriptionId Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new WebhookSubscriptionId(s);
    }

    /// <summary>Parses a character span into a <see cref="WebhookSubscriptionId"/>.</summary>
    public static WebhookSubscriptionId Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace()) throw new ArgumentException("Subscription ID cannot be empty or whitespace.", nameof(s));
        return new WebhookSubscriptionId(s.ToString());
    }

    /// <summary>Tries to parse a string into a <see cref="WebhookSubscriptionId"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookSubscriptionId result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }
        result = new WebhookSubscriptionId(s);
        return true;
    }

    /// <summary>Tries to parse a character span into a <see cref="WebhookSubscriptionId"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out WebhookSubscriptionId result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }
        result = new WebhookSubscriptionId(s.ToString());
        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <inheritdoc/>
    public int CompareTo(WebhookSubscriptionId other) => string.CompareOrdinal(this.Value, other.Value);

    /// <inheritdoc/>
    public int CompareTo(object? obj) => obj is WebhookSubscriptionId other ? CompareTo(other) : 1;

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => this.Value;
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        System.Text.Unicode.Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == System.Buffers.OperationStatus.Done;

    static WebhookSubscriptionId IParsable<WebhookSubscriptionId>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<WebhookSubscriptionId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookSubscriptionId result) => TryParse(s, out result);
    static WebhookSubscriptionId ISpanParsable<WebhookSubscriptionId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<WebhookSubscriptionId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookSubscriptionId result) => TryParse(s, out result);
    static WebhookSubscriptionId IUtf8SpanParsable<WebhookSubscriptionId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(System.Text.Encoding.UTF8.GetString(utf8Text));
    static bool IUtf8SpanParsable<WebhookSubscriptionId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookSubscriptionId result) => TryParse(System.Text.Encoding.UTF8.GetString(utf8Text), out result);
}

/// <summary>JSON converter for <see cref="WebhookSubscriptionId"/>.</summary>
public sealed class WebhookSubscriptionIdJsonConverter : JsonConverter<WebhookSubscriptionId> {
    /// <inheritdoc/>
    public override WebhookSubscriptionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        WebhookSubscriptionId.Parse(reader.GetString()!);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WebhookSubscriptionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}