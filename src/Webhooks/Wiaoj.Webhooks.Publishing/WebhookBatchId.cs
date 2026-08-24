using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents the unique, strongly-typed identifier of a webhook publish batch.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(WebhookBatchIdJsonConverter))]
public readonly record struct WebhookBatchId :
    IEquatable<WebhookBatchId>,
    IComparable<WebhookBatchId>,
    IComparable,
    ISpanParsable<WebhookBatchId>,
    IUtf8SpanParsable<WebhookBatchId>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>Gets the raw string value of the batch identifier.</summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>Gets an equality comparer for <see cref="WebhookBatchId"/> supporting zero-allocation span lookups.</summary>
    public static IEqualityComparer<WebhookBatchId> Comparer { get; } = new BatchIdEqualityComparer();

    /// <summary>Initializes a new instance of the <see cref="WebhookBatchId"/> struct.</summary>
    public WebhookBatchId(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>Creates a new time-ordered unique batch identifier (UUIDv7).</summary>
    public static WebhookBatchId NewId() {
        return new($"batch_{Guid.CreateVersion7():N}");
    }

    public static WebhookBatchId Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new WebhookBatchId(s);
    }

    public static WebhookBatchId Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace()) throw new ArgumentException("Batch ID cannot be empty or whitespace.", nameof(s));
        return new WebhookBatchId(s.ToString());
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookBatchId result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }
        result = new WebhookBatchId(s);
        return true;
    }

    public static bool TryParse(ReadOnlySpan<char> s, out WebhookBatchId result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }
        result = new WebhookBatchId(s.ToString());
        return true;
    }

    public ReadOnlySpan<char> AsSpan() {
        return this.Value.AsSpan();
    }

    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    public static implicit operator WebhookBatchId(string value) {
        return Parse(value);
    }

    public static implicit operator string(WebhookBatchId batchId) {
        return batchId.Value;
    }

    public override string ToString() {
        return this.Value;
    }

    public int CompareTo(WebhookBatchId other) {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    public int CompareTo(object? obj) {
        return obj is WebhookBatchId other ? CompareTo(other) : 1;
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

    static WebhookBatchId IParsable<WebhookBatchId>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<WebhookBatchId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookBatchId result) {
        return TryParse(s, out result);
    }

    static WebhookBatchId ISpanParsable<WebhookBatchId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<WebhookBatchId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookBatchId result) {
        return TryParse(s, out result);
    }

    static WebhookBatchId IUtf8SpanParsable<WebhookBatchId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(Encoding.UTF8.GetString(utf8Text));
    }

    static bool IUtf8SpanParsable<WebhookBatchId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookBatchId result) {
        return TryParse(Encoding.UTF8.GetString(utf8Text), out result);
    }

    private sealed class BatchIdEqualityComparer :
        IEqualityComparer<WebhookBatchId>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookBatchId> {
        public bool Equals(WebhookBatchId x, WebhookBatchId y) {
            return string.Equals(x.Value, y.Value, StringComparison.Ordinal);
        }

        public int GetHashCode(WebhookBatchId obj) {
            return StringComparer.Ordinal.GetHashCode(obj.Value);
        }

        public bool Equals(ReadOnlySpan<char> alternate, WebhookBatchId other) {
            return alternate.Equals(other.Value.AsSpan(), StringComparison.Ordinal);
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            return string.GetHashCode(alternate, StringComparison.Ordinal);
        }

        public WebhookBatchId Create(ReadOnlySpan<char> alternate) {
            return Parse(alternate);
        }
    }
}

public sealed class WebhookBatchIdJsonConverter : JsonConverter<WebhookBatchId> {
    public override WebhookBatchId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return WebhookBatchId.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, WebhookBatchId value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}