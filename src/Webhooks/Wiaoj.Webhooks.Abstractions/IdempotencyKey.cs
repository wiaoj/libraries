using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents an immutable, strongly-typed deterministic idempotency key.
/// </summary>
[DebuggerDisplay("{Value,nq}")]
[JsonConverter(typeof(IdempotencyKeyJsonConverter))]
public readonly record struct IdempotencyKey :
    IEquatable<IdempotencyKey>,
    IComparable<IdempotencyKey>,
    IComparable,
    ISpanParsable<IdempotencyKey>,
    IUtf8SpanParsable<IdempotencyKey>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable {

    private readonly string _value;

    /// <summary>
    /// Gets the raw string representation of the idempotency key.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyKey"/> struct.
    /// </summary>
    /// <param name="value">The key string.</param>
    public IdempotencyKey(string value) {
        Preca.ThrowIfNullOrWhiteSpace(value);
        this._value = value;
    }

    /// <summary>
    /// Creates a deterministic idempotency key using endpoint ID, event name, and a 128-bit payload digest.
    /// </summary>
    /// <param name="endpointId">The destination endpoint identifier.</param>
    /// <param name="eventName">The wire-format event name.</param>
    /// <param name="payloadHash">The 128-bit SIMD hash of the payload.</param>
    /// <returns>A new <see cref="IdempotencyKey"/> instance.</returns>
    public static IdempotencyKey Create(WebhookEndpointId endpointId, string eventName, XxHash128 payloadHash) {
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        int length = 6 + endpointId.Value.Length + 1 + eventName.Length + 1 + (XxHash128.HashSizeInBytes * 2);

        string keyString = string.Create(length, (endpointId.Value, eventName, payloadHash), static (span, state) => {
            "idemp:".AsSpan().CopyTo(span);
            span = span[6..];

            state.Value.AsSpan().CopyTo(span);
            span = span[state.Value.Length..];

            span[0] = ':';
            span = span[1..];

            state.eventName.AsSpan().CopyTo(span);
            span = span[state.eventName.Length..];

            span[0] = ':';
            span = span[1..];

            state.payloadHash.TryFormat(span, out _);
        });

        return new IdempotencyKey(keyString);
    }

    // ── PUBLIC CLEAN API (No IFormatProvider parameter noise) ─────────────────

    /// <summary>
    /// Parses a string into an <see cref="IdempotencyKey"/>.
    /// </summary>
    public static IdempotencyKey Parse(string s) {
        Preca.ThrowIfNullOrWhiteSpace(s);
        return new IdempotencyKey(s);
    }

    /// <summary>
    /// Parses a character span into an <see cref="IdempotencyKey"/>.
    /// </summary>
    public static IdempotencyKey Parse(ReadOnlySpan<char> s) {
        if(s.IsWhiteSpace()) throw new ArgumentException("Idempotency key cannot be empty.", nameof(s));
        return new IdempotencyKey(s.ToString());
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into an <see cref="IdempotencyKey"/>.
    /// </summary>
    public static IdempotencyKey Parse(ReadOnlySpan<byte> utf8Text) {
        string str = System.Text.Encoding.UTF8.GetString(utf8Text);
        return Parse(str);
    }

    /// <summary>
    /// Tries to parse a string into an <see cref="IdempotencyKey"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out IdempotencyKey result) {
        if(string.IsNullOrWhiteSpace(s)) {
            result = default;
            return false;
        }
        result = new IdempotencyKey(s);
        return true;
    }

    /// <summary>
    /// Tries to parse a character span into an <see cref="IdempotencyKey"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out IdempotencyKey result) {
        if(s.IsWhiteSpace()) {
            result = default;
            return false;
        }
        result = new IdempotencyKey(s.ToString());
        return true;
    }

    /// <summary>
    /// Tries to parse a UTF-8 encoded byte span into an <see cref="IdempotencyKey"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out IdempotencyKey result) {
        if(utf8Text.IsEmpty) {
            result = default;
            return false;
        }
        return TryParse(System.Text.Encoding.UTF8.GetString(utf8Text), out result);
    }

    /// <summary>
    /// Formats the idempotency key into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <summary>
    /// Formats the idempotency key into the destination UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return System.Text.Unicode.Utf8.FromUtf16(this.Value.AsSpan(), utf8Destination, out _, out bytesWritten) == System.Buffers.OperationStatus.Done;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    /// <inheritdoc/>
    public int CompareTo(IdempotencyKey other) {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        return obj is IdempotencyKey other ? CompareTo(other) : 1;
    }

    // ── EXPLICIT INTERFACE IMPLEMENTATIONS (Hidden from direct public autocomplete) ──

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(utf8Destination, out bytesWritten);
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    static IdempotencyKey IParsable<IdempotencyKey>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<IdempotencyKey>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IdempotencyKey result) {
        return TryParse(s, out result);
    }

    static IdempotencyKey ISpanParsable<IdempotencyKey>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<IdempotencyKey>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out IdempotencyKey result) {
        return TryParse(s, out result);
    }

    static IdempotencyKey IUtf8SpanParsable<IdempotencyKey>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<IdempotencyKey>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out IdempotencyKey result) {
        return TryParse(utf8Text, out result);
    }
}

/// <summary>
/// JSON converter for <see cref="IdempotencyKey"/>.
/// </summary>
public sealed class IdempotencyKeyJsonConverter : JsonConverter<IdempotencyKey> {
    /// <inheritdoc/>
    public override IdempotencyKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return new(reader.GetString()!);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IdempotencyKey value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}