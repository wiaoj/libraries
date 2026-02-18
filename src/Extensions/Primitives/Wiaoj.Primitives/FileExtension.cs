using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives; 
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(FileExtensionJsonConverter))]
[TypeConverter(typeof(FileExtensionTypeConverter))]
public readonly record struct FileExtension :
    IEquatable<FileExtension>,
    ISpanParsable<FileExtension>,
    ISpanFormattable,
    IUtf8SpanParsable<FileExtension> {
    private static readonly SearchValues<char> InvalidChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());

    private readonly string _value;

    public static FileExtension Empty { get; } = default;
    public string Value => this._value ?? string.Empty;
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

    private FileExtension(string value) {
        this._value = value;
    }

    // --- TEMİZ PUBLIC API (Provider Derdi Yok) ---

    public static FileExtension Parse(string extension) {
        if(TryParse(extension, out FileExtension result)) return result;
        throw new FormatException($"Invalid file extension: '{extension}'");
    }

    public static FileExtension Parse(ReadOnlySpan<char> extension) {
        if(TryParse(extension, out FileExtension result)) return result;
        throw new FormatException($"Invalid file extension: '{extension}'");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out FileExtension result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out FileExtension result) {
        if(s.IsEmpty) { result = Empty; return true; }

        ReadOnlySpan<char> content = s.TrimStart('.');

        if(content.IsEmpty || content.Length > 255 || content.IndexOfAny(InvalidChars) >= 0) {
            result = default;
            return false;
        }

        Span<char> buffer = stackalloc char[content.Length + 1];
        buffer[0] = '.';
        content.ToLowerInvariant(buffer[1..]);

        result = new FileExtension(new string(buffer));
        return true;
    }

    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    // --- EXPLICIT IMPLEMENTATIONS (Arayüzler için arka kapı) ---

    static FileExtension IParsable<FileExtension>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<FileExtension>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FileExtension result) {
        return TryParse(s, out result);
    }

    static FileExtension ISpanParsable<FileExtension>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<FileExtension>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out FileExtension result) {
        return TryParse(s, out result);
    }

    static FileExtension IUtf8SpanParsable<FileExtension>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        if(utf8Text.IsEmpty) return Empty;

        // Uzantılar genelde kısadır, stackalloc güvenli.
        // Encoding.UTF8.GetCharCount Span parametre alır.
        int charCount = System.Text.Encoding.UTF8.GetCharCount(utf8Text);
        Span<char> charBuffer = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];

        // GetChars Span overload'ı int döner, out gerektirmez.
        int written = System.Text.Encoding.UTF8.GetChars(utf8Text, charBuffer);

        return Parse(charBuffer[..written]);
    }

    static bool IUtf8SpanParsable<FileExtension>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out FileExtension result) {
        return TryParseUtf8Internal(utf8Text, out result);
    }

    // ISpanFormattable
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this.Value;
    }

    // --- OPERATORS & OVERRIDES ---

    public override string ToString() {
        return this.Value;
    }

    public static implicit operator string(FileExtension ext) {
        return ext.Value;
    }

    public static explicit operator FileExtension(string ext) {
        return Parse(ext);
    }

    private static bool TryParseUtf8Internal(ReadOnlySpan<byte> utf8Text, out FileExtension result) {
        if(utf8Text.IsEmpty) { result = Empty; return true; }

        // UTF8 -> Char dönüşümü için güvenli buffer
        int charCount = System.Text.Encoding.UTF8.GetCharCount(utf8Text);
        Span<char> charBuffer = charCount <= 256 ? stackalloc char[charCount] : new char[charCount];

        int written = System.Text.Encoding.UTF8.GetChars(utf8Text, charBuffer);

        // Asıl TryParse(ReadOnlySpan<char>) metoduna pasla
        return TryParse(charBuffer[..written], out result);
    }
}

// --- CONVERTERS (Bunları da unutmadım) ---

public class FileExtensionJsonConverter : JsonConverter<FileExtension> {
    public override FileExtension Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return FileExtension.Parse(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, FileExtension value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}

public class FileExtensionTypeConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value) {
        return value is string s ? FileExtension.Parse(s) : base.ConvertFrom(context, culture, value);
    }
}
