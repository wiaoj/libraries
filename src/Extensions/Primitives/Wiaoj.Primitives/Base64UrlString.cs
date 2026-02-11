using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Wiaoj.Primitives; 
/// <summary>
/// Represents a structurally valid Base64Url string (RFC 4648).
/// Immutable and guaranteed to contain only valid Base64Url characters (A-Z, a-z, 0-9, -, _).
/// </summary>
[DebuggerDisplay("{Value}")]
public readonly record struct Base64UrlString : ISpanParsable<Base64UrlString>, ISpanFormattable, IUtf8SpanParsable<Base64UrlString>, IUtf8SpanFormattable {
    // .NET 8 SearchValues ile ultra-hızlı validasyon
    private static readonly SearchValues<char> ValidBase64UrlChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    private readonly string _value;

    /// <summary>
    /// Gets the underlying Base64Url string.
    /// </summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// Returns an empty Base64UrlString.
    /// </summary>
    public static Base64UrlString Empty { get; } = new(string.Empty);

    // Private ctor: Sadece validasyondan geçmiş veri girebilir.
    private Base64UrlString(string value) {
        this._value = value;
    }

    #region Creation (From Bytes)

    /// <summary>
    /// Encodes a byte span into a Base64Url string.
    /// </summary>
    [SkipLocalsInit]
    public static Base64UrlString FromBytes(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) return Empty;

        // Base64Url: Base64 with '-' and '_' instead of '+' and '/' and NO padding.
        int maxLen = Base64.GetMaxEncodedToUtf8Length(bytes.Length);

        // Stackalloc safety: 1KB'a kadar stack, sonrası heap (ArrayPool)
        byte[]? rented = null;
        Span<byte> buffer = maxLen <= 1024
            ? stackalloc byte[maxLen]
            : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

        try {
            OperationStatus status = Base64.EncodeToUtf8(bytes, buffer, out _, out int written, isFinalBlock: true);
            if(status != OperationStatus.Done)
                throw new InvalidOperationException("Failed to encode Base64.");

            // In-place replacement: '+' -> '-', '/' -> '_'
            for(int i = 0; i < written; i++) {
                if(buffer[i] == (byte)'+') buffer[i] = (byte)'-';
                else if(buffer[i] == (byte)'/') buffer[i] = (byte)'_';
            }

            // Remove padding '='
            int actualLen = written;
            while(actualLen > 0 && buffer[actualLen - 1] == (byte)'=') {
                actualLen--;
            }

            return new Base64UrlString(Encoding.UTF8.GetString(buffer[..actualLen]));
        }
        finally {
            if(rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    #endregion

    #region Decoding (To Bytes)

    /// <summary>
    /// Decodes the Base64Url string into a byte span.
    /// </summary>
    [SkipLocalsInit]
    public bool TryDecode(Span<byte> destination, out int bytesWritten) {
        if(string.IsNullOrEmpty(this._value)) {
            bytesWritten = 0;
            return true;
        }

        // Base64Url -> Base64 dönüşümü için geçici buffer lazım.
        // padding eklemek ve karakterleri değiştirmek için string'in kopyasına ihtiyacımız var.
        int requiredLen = this._value.Length + 2; // En fazla 2 padding gelebilir

        char[]? rented = null;
        Span<char> buffer = requiredLen <= 1024
            ? stackalloc char[requiredLen]
            : (rented = ArrayPool<char>.Shared.Rent(requiredLen));

        try {
            // String'i buffera kopyala
            this._value.AsSpan().CopyTo(buffer);
            int len = this._value.Length;

            // Karakterleri geri değiştir: '-' -> '+', '_' -> '/'
            for(int i = 0; i < len; i++) {
                char c = buffer[i];
                if(c == '-') buffer[i] = '+';
                else if(c == '_') buffer[i] = '/';
            }

            // Padding ekle (mod 4)
            while(len % 4 != 0) {
                buffer[len++] = '=';
            }

            return Convert.TryFromBase64Chars(buffer[..len], destination, out bytesWritten);
        }
        finally {
            if(rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    public byte[] ToBytes() {
        if(string.IsNullOrEmpty(this._value)) return [];
        // Tahmini boyut hesabı: (n * 3) / 4
        byte[] buffer = new byte[(this._value.Length * 3 + 3) / 4];
        if(TryDecode(buffer, out int written)) {
            return buffer[..written]; // Tam boyutu döndür (Array resize yapabilir veya yapmayabilir, güvenli olması için slice)
        }
        return [];
    }

    #endregion

    #region Clean Public API (Parsing & Validation)

    public static Base64UrlString Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    public static Base64UrlString Parse(ReadOnlySpan<char> s) {
        if(TryParseInternal(s, out Base64UrlString result)) {
            return result;
        }
        throw new FormatException($"Invalid Base64Url format: '{s}'");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out Base64UrlString result) {
        if(s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out Base64UrlString result) {
        return TryParseInternal(s, out result);
    }

    // Gerçek validasyon burada yapılır
    private static bool TryParseInternal(ReadOnlySpan<char> s, out Base64UrlString result) {
        if(s.IsEmpty) {
            result = Empty;
            return true;
        }

        // 1. Karakter Kontrolü (SIMD Optimized)
        // Eğer geçersiz karakter varsa false dön.
        if(s.IndexOfAnyExcept(ValidBase64UrlChars) >= 0) {
            result = default;
            return false;
        }

        // Base64Url padding içermez ('=' yasak). 
        // Ancak bazı kütüphaneler esnek davranabilir, biz "Strict" olalım:
        // '=' varsa ValidBase64UrlChars içinde olmadığı için zaten yukarıda patlar.

        // 2. Başarılı
        result = new Base64UrlString(s.ToString());
        return true;
    }

    #endregion

    #region Formatting

    public override string ToString() {
        return this.Value;
    }

    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.Value.AsSpan().TryCopyTo(destination)) {
            charsWritten = this.Value.Length;
            return true;
        }
        charsWritten = 0;
        return false;
    }

    #endregion

    #region Explicit Interface Implementation (Hidden Providers)

    // IParsable
    static Base64UrlString IParsable<Base64UrlString>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<Base64UrlString>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Base64UrlString result) {
        return TryParse(s, out result);
    }

    // ISpanParsable
    static Base64UrlString ISpanParsable<Base64UrlString>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<Base64UrlString>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Base64UrlString result) {
        return TryParse(s, out result);
    }

    // IUtf8SpanParsable (Bonus: JSON converter gibi yerlerde byte'tan direkt parse için)
    static Base64UrlString IUtf8SpanParsable<Base64UrlString>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        // ASCII olduğu için byte -> char dönüşümü basittir ama encoding kullanmak güvenlidir.
        return Parse(Encoding.UTF8.GetString(utf8Text).AsSpan());
    }
    static bool IUtf8SpanParsable<Base64UrlString>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Base64UrlString result) {
        return TryParse(Encoding.UTF8.GetString(utf8Text).AsSpan(), out result);
    }

    // ISpanFormattable
    string IFormattable.ToString(string? format, IFormatProvider? provider) {
        return ToString();
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    // IUtf8SpanFormattable
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        if(utf8Destination.Length < this._value.Length) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(this._value, utf8Destination);
        return true;
    }

    #endregion
}