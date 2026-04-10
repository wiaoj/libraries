using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.JsonConverters;

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
        Preca.ThrowIfNull(s);
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

        // 1. SIMD Karakter Kontrolü
        if(trimmed.IndexOfAnyExcept(ValidCronChars) >= 0) {
            result = default;
            return false;
        }

        // 2. İlk Geçiş: Toplam Alan Sayısını Say (Validasyon için kritik)
        int totalFields = 1;
        bool inSpace = false;
        for(int i = 0; i < trimmed.Length; i++) {
            if(char.IsWhiteSpace(trimmed[i])) {
                if(!inSpace) {
                    totalFields++;
                    inSpace = true;
                }
            }
            else inSpace = false;
        }

        if(totalFields is < 5 or > 7) {
            result = default;
            return false;
        }

        // 3. İkinci Geçiş: Segmentleri Ayır ve Mantıksal Validasyon Yap
        int currentFieldIndex = 0;
        int pos = 0;
        while(pos < trimmed.Length) {
            while(pos < trimmed.Length && char.IsWhiteSpace(trimmed[pos])) pos++;
            if(pos >= trimmed.Length) break;

            int start = pos;
            while(pos < trimmed.Length && !char.IsWhiteSpace(trimmed[pos])) pos++;

            ReadOnlySpan<char> segment = trimmed[start..pos];
            currentFieldIndex++;

            // Mantıksal Kontrol
            if(int.TryParse(segment, out int val)) {
                // Artık totalFields biliniyor, IsValidFieldValue doğru çalışacak
                if(!IsValidFieldValue(currentFieldIndex, totalFields, val)) {
                    result = default;
                    return false;
                }
            }
            else {
                char firstChar = segment[0];
                if(!char.IsDigit(firstChar) &&
                    firstChar is not '*' and not '?' and not '/' and not '-' and not 'L' and not 'W' and not '#' and not 'C') {
                    if(!IsValidAlias(segment)) {
                        result = default;
                        return false;
                    }
                }
            }
        }

        result = new CronExpression(trimmed.IndexOf("  ") >= 0
            ? NormalizeSpaces(trimmed)
            : trimmed.ToString());
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidFieldValue(int fieldIndex, int totalFields, int value) {

        int logicalIndex = (totalFields == 5) ? fieldIndex + 1 : fieldIndex;

        return logicalIndex switch {
            1 => value is >= 0 and <= 59,    // Saniye
            2 => value is >= 0 and <= 59,    // Dakika
            3 => value is >= 0 and <= 23,    // Saat
            4 => value is >= 1 and <= 31,    // Gün
            5 => value is >= 1 and <= 12,    // Ay
            6 => value is >= 0 and <= 7,     // Hafta Günü
            7 => value is >= 1970 and <= 2099, // Yıl
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidAlias(ReadOnlySpan<char> segment) {
        if(segment.Length != 3) return false;

        // SWAR (SIMD Within A Register) Tekniği
        // & 0xDF maskesi ASCII harflerini bitwise seviyesinde anında büyük harfe (A-Z) çevirir.
        // Bu sayede StringComparison.OrdinalIgnoreCase maliyetinden kurtuluruz.
        uint c0 = (uint)(segment[0] & 0xDF);
        uint c1 = (uint)(segment[1] & 0xDF);
        uint c2 = (uint)(segment[2] & 0xDF);

        // 3 karakteri birleştirerek tek bir 24-bit (uint) sayı haline getiriyoruz
        uint packed = (c0 << 16) | (c1 << 8) | c2;

        // Artık 3 harfli string kontrolü değil, inanılmaz hızlı bir tam sayı (integer) lookup yapıyoruz.
        return packed switch {
            // --- GÜNLER ---
            0x53554E => true, // SUN (S=0x53, U=0x55, N=0x4E)
            0x4D4F4E => true, // MON
            0x545545 => true, // TUE
            0x574544 => true, // WED
            0x544855 => true, // THU
            0x465249 => true, // FRI
            0x534154 => true, // SAT

            // --- AYLAR ---
            0x4A414E => true, // JAN
            0x464542 => true, // FEB
            0x4D4152 => true, // MAR
            0x415052 => true, // APR
            0x4D4159 => true, // MAY
            0x4A554E => true, // JUN
            0x4A554C => true, // JUL
            0x415547 => true, // AUG
            0x534550 => true, // SEP
            0x4F4354 => true, // OCT
            0x4E4F56 => true, // NOV
            0x444543 => true, // DEC

            // Geçersiz bir kelimeyse (örneğin "abc" -> 0x414243)
            _ => false
        };
    }

    #endregion

    #region Formatting & Operators

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

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
    public static implicit operator string(CronExpression cron) {
        return cron.Value;
    }

    /// <summary>Explicit conversion from string.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator CronExpression(string s) {
        return Parse(s);
    }

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

    static CronExpression IParsable<CronExpression>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<CronExpression>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CronExpression result) {
        return TryParse(s, out result);
    }

    static CronExpression ISpanParsable<CronExpression>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<CronExpression>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CronExpression result) {
        return TryParse(s, out result);
    }

    static CronExpression IUtf8SpanParsable<CronExpression>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<CronExpression>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CronExpression result) {
        return TryParse(utf8Text, out result);
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return ToString();
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(string.IsNullOrEmpty(this._value)) { bytesWritten = 0; return true; }
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Destination);
        return true;
    }

    #endregion
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