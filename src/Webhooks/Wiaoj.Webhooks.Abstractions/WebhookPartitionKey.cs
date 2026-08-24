using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents an immutable, strongly-typed partition routing key used for strict FIFO message ordering across
/// outbox tables, message brokers (Kafka/RabbitMQ), and concurrency delivery locks.
/// </summary>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(WebhookPartitionKeyJsonConverter))]
public readonly record struct WebhookPartitionKey :
    IEquatable<WebhookPartitionKey>,
    IComparable<WebhookPartitionKey>,
    IComparable,
    ISpanParsable<WebhookPartitionKey>,
    IUtf8SpanParsable<WebhookPartitionKey>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    private readonly string _value;

    /// <summary>
    /// Gets the raw string value of the partition key, or an empty string if uninitialized.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Gets a default equality comparer for <see cref="WebhookPartitionKey"/> supporting zero-allocation span lookup.
    /// </summary>
    public static IEqualityComparer<WebhookPartitionKey> Comparer { get; } = new PartitionKeyEqualityComparer();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPartitionKey"/> struct.
    /// </summary>
    /// <param name="value">The partition key string.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public WebhookPartitionKey(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>Creates a partition key directly from an endpoint identifier.</summary>
    public static WebhookPartitionKey From(WebhookEndpointId endpointId) {
        return new(endpointId.Value);
    } 

    /// <summary>Parses a string into a <see cref="WebhookPartitionKey"/>.</summary>
    public static WebhookPartitionKey Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new WebhookPartitionKey(s);
    }

    /// <summary>Parses a character span into a <see cref="WebhookPartitionKey"/>.</summary>
    public static WebhookPartitionKey Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace()) throw new ArgumentException("Partition key cannot be empty or whitespace.", nameof(s));
        return new WebhookPartitionKey(s.ToString());
    }

    /// <summary>Tries to parse a string into a <see cref="WebhookPartitionKey"/>.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out WebhookPartitionKey result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }
        result = new WebhookPartitionKey(s);
        return true;
    }

    /// <summary>Tries to parse a character span into a <see cref="WebhookPartitionKey"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out WebhookPartitionKey result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }
        result = new WebhookPartitionKey(s.ToString());
        return true;
    }

    /// <summary>Formats the partition key into the destination character span.</summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <summary>Formats the partition key into the destination UTF-8 byte span.</summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
    }

    /// <summary>
    /// Returns a direct, zero-allocation character span view over the underlying partition key string.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{Char}"/> representing the partition key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> AsSpan() => this.Value.AsSpan();

    // ── Implicit Operators for Zero Friction ──────────────────────────────────

    /// <summary>Implicitly converts a raw string to a <see cref="WebhookPartitionKey"/>.</summary>
    public static implicit operator WebhookPartitionKey(string value) {
        return Parse(value);
    }

    /// <summary>Implicitly converts a <see cref="WebhookEndpointId"/> to a <see cref="WebhookPartitionKey"/>.</summary>
    public static implicit operator WebhookPartitionKey(WebhookEndpointId endpointId) {
        return From(endpointId);
    }

    /// <summary>Implicitly converts a <see cref="WebhookPartitionKey"/> to its underlying string.</summary>
    public static implicit operator string(WebhookPartitionKey partitionKey) {
        return partitionKey.Value;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public int CompareTo(WebhookPartitionKey other) {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        return obj is WebhookPartitionKey other ? CompareTo(other) : 1;
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

    static WebhookPartitionKey IParsable<WebhookPartitionKey>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<WebhookPartitionKey>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookPartitionKey result) {
        return TryParse(s, out result);
    }

    static WebhookPartitionKey ISpanParsable<WebhookPartitionKey>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<WebhookPartitionKey>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out WebhookPartitionKey result) {
        return TryParse(s, out result);
    }

    static WebhookPartitionKey IUtf8SpanParsable<WebhookPartitionKey>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(Encoding.UTF8.GetString(utf8Text));
    }

    static bool IUtf8SpanParsable<WebhookPartitionKey>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out WebhookPartitionKey result) {
        return TryParse(Encoding.UTF8.GetString(utf8Text), out result);
    }

    private sealed class PartitionKeyEqualityComparer :
        IEqualityComparer<WebhookPartitionKey>,
        IAlternateEqualityComparer<ReadOnlySpan<char>, WebhookPartitionKey> {
        public bool Equals(WebhookPartitionKey x, WebhookPartitionKey y) {
            return string.Equals(x.Value, y.Value, StringComparison.Ordinal);
        }

        public int GetHashCode(WebhookPartitionKey obj) {
            return StringComparer.Ordinal.GetHashCode(obj.Value);
        }

        public bool Equals(ReadOnlySpan<char> alternate, WebhookPartitionKey other) {
            return alternate.Equals(other.Value.AsSpan(), StringComparison.Ordinal);
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            return string.GetHashCode(alternate, StringComparison.Ordinal);
        }

        public WebhookPartitionKey Create(ReadOnlySpan<char> alternate) {
            return Parse(alternate);
        }
    }
}

/// <summary>
/// JSON converter for <see cref="WebhookPartitionKey"/>.
/// </summary>
public sealed class WebhookPartitionKeyJsonConverter : JsonConverter<WebhookPartitionKey> {
    /// <inheritdoc/>
    public override WebhookPartitionKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return WebhookPartitionKey.Parse(reader.GetString()!);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WebhookPartitionKey value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}