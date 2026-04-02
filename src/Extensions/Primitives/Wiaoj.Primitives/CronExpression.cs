using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;
/// <summary>
/// Represents a structurally valid Cron expression (supporting 5 to 7 fields: Linux or Quartz standards).
/// </summary>
/// <remarks>
/// This value object prevents "primitive obsession" by ensuring the given string matches the basic 
/// structural rules of a cron expression (valid characters and correct field count) before it reaches the scheduler.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(CronExpressionJsonConverter))]
[TypeConverter(typeof(CronExpressionTypeConverter))]
public readonly record struct CronExpression :
    IEquatable<CronExpression>,
    ISpanParsable<CronExpression>,
    IUtf8SpanParsable<CronExpression>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable {
    // Allowed characters in a valid Cron expression (Digits, letters for months/days, and symbols * ? / , - L W #)
    private static readonly SearchValues<char> ValidCronChars = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz *?/,-#LWC");
    private static readonly SearchValues<byte> ValidCronBytes = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz *?/,-#LWC"u8);

    private readonly string _value;

    /// <summary>
    /// Gets the underlying Cron expression string.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Represents an empty (uninitialized) Cron expression.
    /// </summary>
    public static CronExpression Empty { get; } = new(string.Empty);

    private CronExpression(string value) {
        this._value = value;
    }

    #region Common Defaults (Factory Constants)

    /// <summary>Runs every minute ("* * * * *").</summary>
    public static CronExpression Minutely { get; } = new("* * * * *");

    /// <summary>Runs at the top of every hour ("0 * * * *").</summary>
    public static CronExpression Hourly { get; } = new("0 * * * *");

    /// <summary>Runs at midnight every day ("0 0 * * *").</summary>
    public static CronExpression Daily { get; } = new("0 0 * * *");

    /// <summary>Runs at midnight on Sunday every week ("0 0 * * 0").</summary>
    public static CronExpression Weekly { get; } = new("0 0 * * 0");

    /// <summary>Runs at midnight on the first day of every month ("0 0 1 * *").</summary>
    public static CronExpression Monthly { get; } = new("0 0 1 * *");

    /// <summary>Runs at midnight on January 1st every year ("0 0 1 1 *").</summary>
    public static CronExpression Yearly { get; } = new("0 0 1 1 *");

    #endregion

    #region Parsing (Public API)

    /// <summary>
    /// Parses a string into a <see cref="CronExpression"/>.
    /// </summary>
    public static CronExpression Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        if(TryParseInternal(s.AsSpan(), out CronExpression result)) return result;
        throw new FormatException($"Invalid Cron expression format: '{s}'");
    }

    /// <summary>
    /// Parses a character span into a <see cref="CronExpression"/>.
    /// </summary>
    public static CronExpression Parse(ReadOnlySpan<char> s) {
        if(TryParseInternal(s, out CronExpression result)) return result;
        throw new FormatException("Invalid Cron expression format.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="CronExpression"/>.
    /// </summary>
    public static CronExpression Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParseInternal(utf8Text, out CronExpression result)) return result;
        throw new FormatException("Invalid Cron expression UTF-8 format.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="CronExpression"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out CronExpression result) {
        if(s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="CronExpression"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out CronExpression result) {
        return TryParseInternal(s, out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="CronExpression"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out CronExpression result) {
        return TryParseInternal(utf8Text, out result);
    }

    #endregion

    #region Internal Validation Logic (Zero-Allocation)

    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out CronExpression result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            result = Empty;
            return true;
        }

        // 1. SIMD Character Validation (Fast rejection of garbage)
        if(trimmed.IndexOfAnyExcept(ValidCronChars) >= 0) {
            result = default;
            return false;
        }

        // 2. Structural Segment Validation (Count fields separated by spaces)
        int fieldCount = 1;
        bool inSpace = false;

        for(int i = 0; i < trimmed.Length; i++) {
            if(char.IsWhiteSpace(trimmed[i])) {
                if(!inSpace) {
                    fieldCount++;
                    inSpace = true;
                }
            }
            else {
                inSpace = false;
            }
        }

        // Linux Cron = 5 fields. Quartz Cron = 6 or 7 fields.
        if(fieldCount < 5 || fieldCount > 7) {
            result = default;
            return false;
        }

        // Collapse multiple spaces to a single space to normalize the value
        if(trimmed.IndexOf("  ") >= 0) {
            // Fallback to allocating a normalized string if dirty spaces exist
            result = new CronExpression(NormalizeSpaces(trimmed));
            return true;
        }

        result = new CronExpression(trimmed.ToString());
        return true;
    }

    [SkipLocalsInit]
    private static bool TryParseInternal(ReadOnlySpan<byte> utf8Text, out CronExpression result) {
        // Trim standard ASCII whitespace bytes
        while(utf8Text.Length > 0 && utf8Text[0] <= 32) utf8Text = utf8Text[1..];
        while(utf8Text.Length > 0 && utf8Text[^1] <= 32) utf8Text = utf8Text[..^1];

        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        // SIMD validation
        if(utf8Text.IndexOfAnyExcept(ValidCronBytes) >= 0) {
            result = default;
            return false;
        }

        // Convert safe UTF8 to Char Span for structural validation
        int charCount = Encoding.UTF8.GetCharCount(utf8Text);
        Span<char> charBuffer = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];
        int written = Encoding.UTF8.GetChars(utf8Text, charBuffer);

        return TryParseInternal(charBuffer[..written], out result);
    }

    private static string NormalizeSpaces(ReadOnlySpan<char> s) {
        Span<char> buffer = stackalloc char[s.Length];
        int index = 0;
        bool inSpace = false;

        for(int i = 0; i < s.Length; i++) {
            char c = s[i];
            if(char.IsWhiteSpace(c)) {
                if(!inSpace) {
                    buffer[index++] = ' ';
                    inSpace = true;
                }
            }
            else {
                buffer[index++] = c;
                inSpace = false;
            }
        }

        return buffer[..index].ToString();
    }

    #endregion

    #region Formatting & Operators

    /// <inheritdoc/>
    public override string ToString() => this.Value;

    /// <summary>
    /// Attempts to format the value of the current instance into the provided span of characters.
    /// </summary>
    /// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    /// <summary>Implicit conversion to string.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(CronExpression cron) => cron.Value;

    /// <summary>Explicit conversion from string.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator CronExpression(string s) => Parse(s);

    /// <inheritdoc/>
    public bool Equals(CronExpression other) {
        return string.Equals(this.Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override int GetHashCode() { 
        return StringComparer.OrdinalIgnoreCase.GetHashCode(this.Value);
    }

    #endregion

    #region Explicit Interface Implementations (Hidden API)

    static CronExpression IParsable<CronExpression>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<CronExpression>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CronExpression result) => TryParse(s, out result);

    static CronExpression ISpanParsable<CronExpression>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<CronExpression>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CronExpression result) => TryParse(s, out result);

    static CronExpression IUtf8SpanParsable<CronExpression>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<CronExpression>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CronExpression result) => TryParse(utf8Text, out result);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Destination);
        return true;
    }

    #endregion
}

#region Converters

/// <inheritdoc/>
public sealed class CronExpressionJsonConverter : JsonConverter<CronExpression> {
    /// <inheritdoc/>
    public override CronExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string token for CronExpression.");

        // Span-based zero allocation read if possible
        ReadOnlySpan<byte> span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
        if(CronExpression.TryParse(span, out CronExpression result))
            return result;

        throw new JsonException($"'{reader.GetString()}' is not a valid CronExpression.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CronExpression value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}

/// <inheritdoc/>
public sealed class CronExpressionTypeConverter : TypeConverter {
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value) {
        if(value is string str) {
            return CronExpression.Parse(str);
        }
        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType) {
        if(destinationType == typeof(string) && value is CronExpression cron) {
            return cron.Value;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

#endregion