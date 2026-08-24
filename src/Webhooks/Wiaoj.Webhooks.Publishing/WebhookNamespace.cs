using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents an immutable, strongly-typed logical isolation namespace or tenant boundary for webhook subscriptions.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(WebhookNamespaceJsonConverter))]
public readonly record struct WebhookNamespace :
    IEquatable<WebhookNamespace>,
    IComparable<WebhookNamespace>,
    IComparable,
    ISpanParsable<WebhookNamespace>,
    IUtf8SpanParsable<WebhookNamespace>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>Gets the raw string value of the namespace, or an empty string if uninitialized.</summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>Gets the default global namespace used when no specific isolation scope is defined.</summary>
    public static WebhookNamespace Default { get; } = new("default");

    /// <summary>Initializes a new instance of the <see cref="WebhookNamespace"/> struct.</summary>
    public WebhookNamespace(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>Parses a string into a <see cref="WebhookNamespace"/>.</summary>
    public static WebhookNamespace Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new WebhookNamespace(s);
    }

    /// <summary>Parses a character span into a <see cref="WebhookNamespace"/>.</summary>
    public static WebhookNamespace Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace())
            throw new ArgumentException("Namespace cannot be empty or whitespace.", nameof(s));
        return new WebhookNamespace(s.ToString());
    }

    /// <summary>Tries to parse a string into a <see cref="WebhookNamespace"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookNamespace result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }
        result = new WebhookNamespace(s);
        return true;
    }

    /// <summary>Tries to parse a character span into a <see cref="WebhookNamespace"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out WebhookNamespace result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }
        result = new WebhookNamespace(s.ToString());
        return true;
    }

    /// <summary>Returns a direct zero-allocation character span view over the namespace value.</summary>
    public ReadOnlySpan<char> AsSpan() {
        return this.Value.AsSpan();
    }

    /// <summary>Formats the namespace into the destination character span.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <summary>Formats the namespace into the destination UTF-8 byte span.</summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == System.Buffers.OperationStatus.Done;
    }

    // Implicit conversions for developer convenience
    public static implicit operator WebhookNamespace(string value) {
        return Parse(value);
    }

    public static implicit operator string(WebhookNamespace @namespace) {
        return @namespace.Value;
    }

    public override string ToString() {
        return this.Value;
    }

    public int CompareTo(WebhookNamespace other) {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    public int CompareTo(object? obj) {
        return obj is WebhookNamespace other ? CompareTo(other) : 1;
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(utf8Destination, out bytesWritten);
    }

    static WebhookNamespace IParsable<WebhookNamespace>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<WebhookNamespace>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookNamespace result) {
        return TryParse(s, out result);
    }

    static WebhookNamespace ISpanParsable<WebhookNamespace>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<WebhookNamespace>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookNamespace result) {
        return TryParse(s, out result);
    }

    static WebhookNamespace IUtf8SpanParsable<WebhookNamespace>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(System.Text.Encoding.UTF8.GetString(utf8Text));
    }

    static bool IUtf8SpanParsable<WebhookNamespace>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookNamespace result) {
        return TryParse(System.Text.Encoding.UTF8.GetString(utf8Text), out result);
    }
}

/// <summary>JSON converter for <see cref="WebhookNamespace"/>.</summary>
public sealed class WebhookNamespaceJsonConverter : JsonConverter<WebhookNamespace> {
    public override WebhookNamespace Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return WebhookNamespace.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, WebhookNamespace value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}