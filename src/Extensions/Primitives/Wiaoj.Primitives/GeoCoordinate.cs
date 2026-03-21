using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(GeoCoordinateJsonConverter))]
[Experimental("WP_GEO_COORDINATE")]
public readonly record struct GeoCoordinate :
    IEquatable<GeoCoordinate>,
    ISpanParsable<GeoCoordinate>,
    ISpanFormattable,
    IUtf8SpanFormattable {

    public double Latitude { get; }
    public double Longitude { get; }

    private readonly bool _initialized;
    public bool IsEmpty => !_initialized;

    private GeoCoordinate(double latitude, double longitude) {
        Latitude = latitude;
        Longitude = longitude;
        _initialized = true;
    }

    #region Factory

    public static GeoCoordinate Create(double latitude, double longitude) {
        if(!IsValidLatitude(latitude))
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be in [-90, 90].");
        if(!IsValidLongitude(longitude))
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be in [-180, 180].");
        return new(latitude, longitude);
    }

    public static bool TryCreate(double latitude, double longitude, out GeoCoordinate result) {
        if(IsValidLatitude(latitude) && IsValidLongitude(longitude)) {
            result = new(latitude, longitude);
            return true;
        }
        result = default;
        return false;
    }

    public static GeoCoordinate Origin { get; } = new(0.0, 0.0);
    public static GeoCoordinate NorthPole { get; } = new(90.0, 0.0);
    public static GeoCoordinate SouthPole { get; } = new(-90.0, 0.0);

    #endregion

    #region Distance (Haversine)

    public double DistanceTo(GeoCoordinate other) {
        const double R = 6_371_000.0;
        double lat1 = ToRad(Latitude), lat2 = ToRad(other.Latitude);
        double dLat = ToRad(other.Latitude - Latitude);
        double dLon = ToRad(other.Longitude - Longitude);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ToRad(double deg) => deg * (Math.PI / 180.0);

    #endregion

    #region Parsing

    public static GeoCoordinate Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    public static GeoCoordinate Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out GeoCoordinate r)) return r;
        throw new FormatException($"'{s}' is not a valid GeoCoordinate. Expected 'lat,lng'.");
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out GeoCoordinate result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out GeoCoordinate result) {
        int comma = s.IndexOf(',');
        if(comma < 1 || comma >= s.Length - 1) { result = default; return false; }
        if(!double.TryParse(s[..comma].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
           !double.TryParse(s[(comma + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) {
            result = default; return false;
        }
        return TryCreate(lat, lon, out result);
    }

    static GeoCoordinate IParsable<GeoCoordinate>.Parse(string s, IFormatProvider? p) => Parse(s);
    static bool IParsable<GeoCoordinate>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, out GeoCoordinate r) => TryParse(s, out r);
    static GeoCoordinate ISpanParsable<GeoCoordinate>.Parse(ReadOnlySpan<char> s, IFormatProvider? p) => Parse(s);
    static bool ISpanParsable<GeoCoordinate>.TryParse(ReadOnlySpan<char> s, IFormatProvider? p, out GeoCoordinate r) => TryParse(s, out r);

    #endregion

    #region Formatting

    public override string ToString() => ToString("F6");

    public string ToString(string? format) {
        string f = format ?? "F6";
        return $"{Latitude.ToString(f, CultureInfo.InvariantCulture)},{Longitude.ToString(f, CultureInfo.InvariantCulture)}";
    }

    string IFormattable.ToString(string? format, IFormatProvider? p) => ToString(format);

    bool ISpanFormattable.TryFormat(Span<char> dest, out int written, ReadOnlySpan<char> format, IFormatProvider? p) {
        string f = format.IsEmpty ? "F6" : format.ToString();
        Span<char> latBuf = stackalloc char[32], lonBuf = stackalloc char[32];
        if(!Latitude.TryFormat(latBuf, out int latLen, f, CultureInfo.InvariantCulture) ||
           !Longitude.TryFormat(lonBuf, out int lonLen, f, CultureInfo.InvariantCulture)) {
            written = 0; return false;
        }
        int req = latLen + 1 + lonLen;
        if(dest.Length < req) { written = 0; return false; }
        latBuf[..latLen].CopyTo(dest);
        dest[latLen] = ',';
        lonBuf[..lonLen].CopyTo(dest[(latLen + 1)..]);
        written = req;
        return true;
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Dest, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? p) {
        Span<char> buf = stackalloc char[64];
        if(!((ISpanFormattable)this).TryFormat(buf, out int len, format, p)) { bytesWritten = 0; return false; }
        if(utf8Dest.Length < len) { bytesWritten = 0; return false; }
        bytesWritten = System.Text.Encoding.UTF8.GetBytes(buf[..len], utf8Dest);
        return true;
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidLatitude(double v) => !double.IsNaN(v) && v >= -90.0 && v <= 90.0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidLongitude(double v) => !double.IsNaN(v) && v >= -180.0 && v <= 180.0;

    public void Deconstruct(out double lat, out double lng) { lat = Latitude; lng = Longitude; }
    public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);
}

#pragma warning disable WP_GEO_COORDINATE // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public sealed class GeoCoordinateJsonConverter : JsonConverter<GeoCoordinate> {
#pragma warning restore WP_GEO_COORDINATE // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable WP_GEO_COORDINATE // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public override GeoCoordinate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            string? s = reader.GetString();
            if(s is not null && GeoCoordinate.TryParse(s, out GeoCoordinate r)) return r;
            throw new JsonException($"Cannot parse '{s}' as GeoCoordinate.");
        }
        if(reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected object or string for GeoCoordinate.");
        double lat = 0, lng = 0;
        while(reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
            if(reader.TokenType != JsonTokenType.PropertyName) continue;
            string? prop = reader.GetString(); reader.Read();
            if(prop is "lat" or "latitude") lat = reader.GetDouble();
            else if(prop is "lng" or "longitude") lng = reader.GetDouble();
        }
        if(!GeoCoordinate.TryCreate(lat, lng, out GeoCoordinate result))
            throw new JsonException($"Invalid GeoCoordinate: lat={lat}, lng={lng}.");
        return result;
    }
#pragma warning restore WP_GEO_COORDINATE // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#pragma warning disable WP_GEO_COORDINATE // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public override void Write(Utf8JsonWriter writer, GeoCoordinate value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteNumber("lat", value.Latitude);
        writer.WriteNumber("lng", value.Longitude);
        writer.WriteEndObject();
    }
#pragma warning restore WP_GEO_COORDINATE // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}