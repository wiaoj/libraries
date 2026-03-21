using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Samples.Primitives;
/// <summary>
/// Represents a validated MIME type (e.g., "application/json", "image/png").
/// </summary>
/// <remarks>
/// <para>
/// Enforces the structural rule: <c>type/subtype</c> where both parts contain
/// only valid token characters (RFC 7230). Does not validate against the IANA registry —
/// custom types like "application/vnd.mycompany.v1+json" are accepted.
/// </para>
/// <para>
/// Well-known types are available as static properties: <see cref="MimeType.ApplicationJson"/>,
/// <see cref="MimeType.TextHtml"/>, etc.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(MimeTypeJsonConverter))]
public readonly record struct MimeType :
    IEquatable<MimeType>,
    ISpanParsable<MimeType>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    // RFC 7230 token characters — everything except separators
    private static readonly SearchValues<char> TokenChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-.^_`|~");

    private readonly string _value; // stored as "type/subtype" always lowercase

    /// <summary>Gets the full MIME type string (e.g., "application/json").</summary>
    public string Value => this._value ?? string.Empty;

    /// <summary>Gets the type part (e.g., "application" from "application/json").</summary>
    public ReadOnlySpan<char> Type {
        get {
            ReadOnlySpan<char> s = this.Value.AsSpan();
            int slash = s.IndexOf('/');
            return slash >= 0 ? s[..slash] : s;
        }
    }

    /// <summary>Gets the subtype part (e.g., "json" from "application/json").</summary>
    public ReadOnlySpan<char> Subtype {
        get {
            ReadOnlySpan<char> s = this.Value.AsSpan();
            int slash = s.IndexOf('/');
            return slash >= 0 ? s[(slash + 1)..] : ReadOnlySpan<char>.Empty;
        }
    }

    /// <summary>
    /// Gets whether this MIME type has a structured suffix (e.g., "+json" in "application/vnd.api+json").
    /// </summary>
    public bool HasSuffix => this.Value.Contains('+');

    private MimeType(string value) {
        this._value = value;
    }

    #region Well-Known Types

    // Application
    public static MimeType ApplicationJson { get; } = new("application/json");
    public static MimeType ApplicationXml { get; } = new("application/xml");
    public static MimeType ApplicationPdf { get; } = new("application/pdf");
    public static MimeType ApplicationOctetStream { get; } = new("application/octet-stream");
    public static MimeType ApplicationFormUrlEncoded { get; } = new("application/x-www-form-urlencoded");
    public static MimeType ApplicationJsonPatch { get; } = new("application/json-patch+json");
    public static MimeType ApplicationProblemJson { get; } = new("application/problem+json");
    public static MimeType ApplicationZip { get; } = new("application/zip");
    public static MimeType ApplicationGzip { get; } = new("application/gzip");

    // Text
    public static MimeType TextPlain { get; } = new("text/plain");
    public static MimeType TextHtml { get; } = new("text/html");
    public static MimeType TextCss { get; } = new("text/css");
    public static MimeType TextCsv { get; } = new("text/csv");
    public static MimeType TextJavascript { get; } = new("text/javascript");
    public static MimeType TextXml { get; } = new("text/xml");
    public static MimeType TextMarkdown { get; } = new("text/markdown");

    // Image
    public static MimeType ImagePng { get; } = new("image/png");
    public static MimeType ImageJpeg { get; } = new("image/jpeg");
    public static MimeType ImageGif { get; } = new("image/gif");
    public static MimeType ImageWebp { get; } = new("image/webp");
    public static MimeType ImageSvg { get; } = new("image/svg+xml");
    public static MimeType ImageAvif { get; } = new("image/avif");
    public static MimeType ImageIco { get; } = new("image/x-icon");

    // Audio / Video
    public static MimeType AudioMpeg { get; } = new("audio/mpeg");
    public static MimeType AudioOgg { get; } = new("audio/ogg");
    public static MimeType VideoMp4 { get; } = new("video/mp4");
    public static MimeType VideoWebm { get; } = new("video/webm");

    // Multipart
    public static MimeType MultipartFormData { get; } = new("multipart/form-data");

    // Font
    public static MimeType FontWoff2 { get; } = new("font/woff2");

    #endregion

    #region Helpers

    /// <summary>Returns <see langword="true"/> if this is any text/* MIME type.</summary>
    public bool IsText => this.Type.Equals("text", StringComparison.Ordinal);

    /// <summary>Returns <see langword="true"/> if this is any image/* MIME type.</summary>
    public bool IsImage => this.Type.Equals("image", StringComparison.Ordinal);

    /// <summary>Returns <see langword="true"/> if this is any audio/* MIME type.</summary>
    public bool IsAudio => this.Type.Equals("audio", StringComparison.Ordinal);

    /// <summary>Returns <see langword="true"/> if this is any video/* MIME type.</summary>
    public bool IsVideo => this.Type.Equals("video", StringComparison.Ordinal);

    /// <summary>Returns <see langword="true"/> if this is any application/* MIME type.</summary>
    public bool IsApplication => this.Type.Equals("application", StringComparison.Ordinal);

    #endregion

    #region Parsing

    /// <summary>Parses a MIME type string (e.g., "application/json").</summary>
    public static MimeType Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>Parses a MIME type character span.</summary>
    public static MimeType Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out MimeType result)) return result;
        throw new FormatException($"'{s}' is not a valid MIME type. Expected 'type/subtype' format.");
    }

    /// <summary>Tries to parse a MIME type string.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out MimeType result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse a MIME type character span.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out MimeType result) {
        // Trim optional parameters (e.g., "text/html; charset=utf-8" → "text/html")
        int semicolon = s.IndexOf(';');
        if(semicolon >= 0) s = s[..semicolon].TrimEnd();

        s = s.Trim();

        int slash = s.IndexOf('/');
        if(slash < 1 || slash >= s.Length - 1) { result = default; return false; }

        ReadOnlySpan<char> type = s[..slash];
        ReadOnlySpan<char> subtype = s[(slash + 1)..];

        // Validate both parts contain only RFC 7230 token chars
        if(type.IndexOfAnyExcept(TokenChars) >= 0 ||
           subtype.IndexOfAnyExcept(TokenChars) >= 0) {
            result = default;
            return false;
        }

        // Normalize to lowercase
        string normalized = s.ToString().ToLowerInvariant();
        result = new MimeType(normalized);
        return true;
    }

    static MimeType IParsable<MimeType>.Parse(string s, IFormatProvider? p) {
        return Parse(s);
    }

    static bool IParsable<MimeType>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, out MimeType r) {
        return TryParse(s, out r);
    }

    static MimeType ISpanParsable<MimeType>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) {
        return Parse(s);
    }

    static bool ISpanParsable<MimeType>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out MimeType r) {
        return TryParse(s, out r);
    }

    #endregion

    #region Formatting

    /// <inheritdoc/>
    public override string ToString() {
        return this.Value;
    }

    string IFormattable.ToString(string? format, IFormatProvider? p) {
        return this.Value;
    }

    bool ISpanFormattable.TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p) {
        ReadOnlySpan<char> src = this.Value.AsSpan();
        if(dest.Length < src.Length) { written = 0; return false; }
        src.CopyTo(dest);
        written = src.Length;
        return true;
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p) {
        if(string.IsNullOrEmpty(this._value)) { written = 0; return true; }
        if(utf8Dest.Length < this._value.Length) { written = 0; return false; }
        written = System.Text.Encoding.UTF8.GetBytes(this._value.AsSpan(), utf8Dest);
        return true;
    }

    #endregion

    #region Equality & Operators

    /// <inheritdoc/>
    public bool Equals(MimeType other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this.Value.GetHashCode(StringComparison.Ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(MimeType m) {
        return m.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator MimeType(string s) {
        return Parse(s);
    }

    #endregion
}

public sealed class MimeTypeJsonConverter : JsonConverter<MimeType> {
    public override MimeType Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o) {
        string? s = reader.GetString();
        if(s is not null && MimeType.TryParse(s, out MimeType result)) return result;
        throw new JsonException($"'{s}' is not a valid MIME type.");
    }
    public override void Write(Utf8JsonWriter writer, MimeType value, JsonSerializerOptions o) {
        writer.WriteStringValue(value.Value);
    }
}