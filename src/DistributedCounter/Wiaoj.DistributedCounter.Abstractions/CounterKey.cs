using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Represents a strongly-typed, validated key for a distributed counter.
/// Prevents passing raw strings and ensures consistent key formatting (e.g., lowercase, trimmed).
/// </summary>
[DebuggerDisplay("{Value}")]
public readonly record struct CounterKey :
    IEquatable<CounterKey>,
    ISpanParsable<CounterKey>,
    IUtf8SpanParsable<CounterKey> {

    private readonly string _value;

    /// <summary>
    /// Gets the underlying string representation of the key.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the key is null or empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    /// <summary>
    /// Represents an empty counter key.
    /// </summary>
    public static CounterKey Empty { get; } = new(string.Empty);

    private CounterKey(string value) {
        this._value = value;
    }

    /// <summary>
    /// Parses a string into a <see cref="CounterKey"/>.
    /// Trims the input and ensures it is not empty.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    public static CounterKey Parse(string s) {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);
        return new CounterKey(s.Trim());
    }

    /// <summary>
    /// Parses a character span into a <see cref="CounterKey"/>.
    /// Trims leading and trailing whitespace.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A validated <see cref="CounterKey"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the span is empty or contains only whitespace.</exception>
    public static CounterKey Parse(ReadOnlySpan<char> s) {
        // Önce trim yapıyoruz (bu işlem yeni bir string oluşturmaz, sadece span sınırlarını değiştirir)
        ReadOnlySpan<char> trimmed = s.Trim();

        // Trim edilmiş hali boşsa, sadece boşluklardan oluşuyor demektir
        if(trimmed.IsEmpty) {
            throw new ArgumentException("Key cannot be empty or consist only of whitespace.", nameof(s));
        }

        // Sadece geçerli kısımdan tek bir string allocate ediyoruz
        return new CounterKey(trimmed.ToString());
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into a <see cref="CounterKey"/>.
    /// </summary>
    public static CounterKey Parse(ReadOnlySpan<byte> utf8Text) {
        ReadOnlySpan<byte> trimmed = utf8Text.Trim(" \t\n\r"u8);

        if(trimmed.IsEmpty) throw new ArgumentException("Key cannot be empty.");

        return new CounterKey(Encoding.UTF8.GetString(trimmed));
    }

    /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, out TSelf)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CounterKey result) {
        if(string.IsNullOrWhiteSpace(s)) { result = default; return false; }
        result = new CounterKey(s.Trim());
        return true;
    }

    /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)"/>
    public static bool TryParse(ReadOnlySpan<char> s, out CounterKey result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            result = default;
            return false;
        }
        result = new CounterKey(trimmed.ToString());
        return true;
    }

    /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.TryParse(ReadOnlySpan{byte}, IFormatProvider?, out TSelf)"/>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out CounterKey result) {
        if(utf8Text.IsEmpty) { result = default; return false; }
        try {
            result = new CounterKey(Encoding.UTF8.GetString(utf8Text).Trim());
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Implicitly converts a string to a <see cref="CounterKey"/>.
    /// </summary>
    public static implicit operator CounterKey(string s) {
        return Parse(s);
    }

    /// <summary>
    /// Implicitly converts a <see cref="CounterKey"/> to its string value.
    /// </summary>
    public static implicit operator string(CounterKey k) {
        return k.Value;
    }

    /// <summary>
    /// Returns the string representation of the key.
    /// </summary>
    public override string ToString() {
        return this.Value;
    }

    // 1. IParsable<CounterKey>
    static CounterKey IParsable<CounterKey>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<CounterKey>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CounterKey result) {
        return TryParse(s, out result);
    }

    static CounterKey ISpanParsable<CounterKey>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<CounterKey>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CounterKey result) {
        return TryParse(s, out result);
    }

    // YENİ: IUtf8SpanParsable Explicit
    static CounterKey IUtf8SpanParsable<CounterKey>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<CounterKey>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CounterKey result) {
        return TryParse(utf8Text, out result);
    }
}