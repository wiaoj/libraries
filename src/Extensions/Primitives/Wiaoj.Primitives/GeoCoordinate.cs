using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives;

/// <summary>
/// Represents an immutable geographic coordinate (latitude and longitude).
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(GeoCoordinateJsonConverter))]
[Experimental("WP_GEO_COORDINATE")]
public readonly record struct GeoCoordinate :
    IEquatable<GeoCoordinate>,
    IParsable<GeoCoordinate>,
    ISpanParsable<GeoCoordinate>,
    IUtf8SpanParsable<GeoCoordinate>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable {

    /// <summary>Gets the latitude in degrees [-90, 90].</summary>
    public double Latitude { get; }

    /// <summary>Gets the longitude in degrees [-180, 180].</summary>
    public double Longitude { get; }

    private readonly bool _initialized;

    /// <summary>Gets a value indicating whether this coordinate is uninitialized (empty).</summary>
    public bool IsEmpty => !_initialized;

    private GeoCoordinate(double latitude, double longitude) {
        Latitude = latitude;
        Longitude = longitude;
        _initialized = true;
    }

    #region Factory

    /// <summary>
    /// Creates a new <see cref="GeoCoordinate"/> with the specified latitude and longitude.
    /// </summary>
    /// <param name="latitude">The latitude in degrees [-90, 90].</param>
    /// <param name="longitude">The longitude in degrees [-180, 180].</param>
    /// <returns>A new <see cref="GeoCoordinate"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when latitude or longitude is out of valid range.</exception>
    public static GeoCoordinate Create(double latitude, double longitude) {
        if(!IsValidLatitude(latitude))
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be in [-90, 90].");
        if(!IsValidLongitude(longitude))
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be in [-180, 180].");
        return new(latitude, longitude);
    }

    /// <summary>
    /// Attempts to create a new <see cref="GeoCoordinate"/> with the specified latitude and longitude.
    /// </summary>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="result">When this method returns, contains the created coordinate if valid; otherwise, default.</param>
    /// <returns><see langword="true"/> if creation succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(double latitude, double longitude, out GeoCoordinate result) {
        if(IsValidLatitude(latitude) && IsValidLongitude(longitude)) {
            result = new(latitude, longitude);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>Represents coordinate at origin (0, 0).</summary>
    public static GeoCoordinate Origin { get; } = new(0.0, 0.0);

    /// <summary>Represents coordinate at the North Pole (90, 0).</summary>
    public static GeoCoordinate NorthPole { get; } = new(90.0, 0.0);

    /// <summary>Represents coordinate at the South Pole (-90, 0).</summary>
    public static GeoCoordinate SouthPole { get; } = new(-90.0, 0.0);

    #endregion

    #region Distance (Haversine)

    /// <summary>
    /// Computes the great-circle distance between this coordinate and another in meters using the Haversine formula.
    /// </summary>
    /// <param name="other">The target coordinate.</param>
    /// <returns>The distance in meters.</returns>
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

    /// <summary>
    /// Parses a string into a <see cref="GeoCoordinate"/>.
    /// </summary>
    public static GeoCoordinate Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="GeoCoordinate"/>.
    /// </summary>
    public static GeoCoordinate Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out GeoCoordinate r)) return r;
        throw new FormatException($"'{s}' is not a valid GeoCoordinate. Expected 'lat,lng'.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="GeoCoordinate"/>.
    /// </summary>
    public static GeoCoordinate Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out GeoCoordinate r)) return r;
        throw new FormatException("Invalid UTF-8 sequence for GeoCoordinate. Expected 'lat,lng'.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="GeoCoordinate"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out GeoCoordinate result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="GeoCoordinate"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out GeoCoordinate result) {
        int comma = s.IndexOf(',');
        if(comma < 1 || comma >= s.Length - 1) { result = default; return false; }
        if(!double.TryParse(s[..comma].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
           !double.TryParse(s[(comma + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) {
            result = default; return false;
        }
        return TryCreate(lat, lon, out result);
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="GeoCoordinate"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out GeoCoordinate result) {
        if(utf8Text.IsEmpty) { result = default; return false; }
        Span<char> chars = stackalloc char[utf8Text.Length <= 64 ? utf8Text.Length : 64];
        char[]? rented = utf8Text.Length > 64 ? System.Buffers.ArrayPool<char>.Shared.Rent(utf8Text.Length) : null;
        Span<char> buf = rented is not null ? rented.AsSpan(0, utf8Text.Length) : chars;
        try {
            if(System.Text.Encoding.UTF8.GetChars(utf8Text, buf) == utf8Text.Length) {
                return TryParse(buf, out result);
            }
            result = default;
            return false;
        }
        finally {
            if(rented is not null) System.Buffers.ArrayPool<char>.Shared.Return(rented);
        }
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static GeoCoordinate IParsable<GeoCoordinate>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<GeoCoordinate>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out GeoCoordinate result) => TryParse(s, out result);
    static GeoCoordinate ISpanParsable<GeoCoordinate>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<GeoCoordinate>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out GeoCoordinate result) => TryParse(s, out result);
    static GeoCoordinate IUtf8SpanParsable<GeoCoordinate>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<GeoCoordinate>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out GeoCoordinate result) => TryParse(utf8Text, out result);

    #endregion

    #region Formatting

    /// <inheritdoc/>
    public override string ToString() => ToString("F6", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats the geo coordinate using the specified format.
    /// </summary>
    public string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats the geo coordinate using the specified format and format provider.
    /// </summary>
    public string ToString(string? format, IFormatProvider? provider) {
        string f = format ?? "F6";
        IFormatProvider prov = provider ?? CultureInfo.InvariantCulture;
        return $"{this.Latitude.ToString(f, prov)},{this.Longitude.ToString(f, prov)}";
    }

    /// <summary>
    /// Tries to format the geo coordinate into the destination character span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Tries to format the geo coordinate into the destination character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Tries to format the geo coordinate into the destination character span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        string f = format.IsEmpty ? "F6" : format.ToString();
        IFormatProvider prov = provider ?? CultureInfo.InvariantCulture;
        Span<char> latBuf = stackalloc char[32], lonBuf = stackalloc char[32];
        if(!this.Latitude.TryFormat(latBuf, out int latLen, f, prov) ||
           !this.Longitude.TryFormat(lonBuf, out int lonLen, f, prov)) {
            charsWritten = 0; return false;
        }
        int req = latLen + 1 + lonLen;
        if(destination.Length < req) { charsWritten = 0; return false; }
        latBuf[..latLen].CopyTo(destination);
        destination[latLen] = ',';
        lonBuf[..lonLen].CopyTo(destination[(latLen + 1)..]);
        charsWritten = req;
        return true;
    }

    /// <summary>
    /// Tries to format the geo coordinate into the destination UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, null);

    /// <summary>
    /// Tries to format the geo coordinate into the destination UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Tries to format the geo coordinate into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        Span<char> buf = stackalloc char[64];
        if(!TryFormat(buf, out int len, format, provider)) { bytesWritten = 0; return false; }
        if(utf8Destination.Length < len) { bytesWritten = 0; return false; }
        bytesWritten = System.Text.Encoding.UTF8.GetBytes(buf[..len], utf8Destination);
        return true;
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs equality comparisons on <see cref="GeoCoordinate"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<GeoCoordinate> OrdinalComparer => GeoCoordinateOrdinalComparer.Instance;

    private sealed class GeoCoordinateOrdinalComparer : IEqualityComparer<GeoCoordinate>, IAlternateEqualityComparer<ReadOnlySpan<char>, GeoCoordinate> {
        public static GeoCoordinateOrdinalComparer Instance { get; } = new();

        public bool Equals(GeoCoordinate x, GeoCoordinate y) => x.Equals(y);

        public int GetHashCode(GeoCoordinate obj) => obj.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, GeoCoordinate other) {
            if(GeoCoordinate.TryParse(alternate, out GeoCoordinate parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(GeoCoordinate.TryParse(alternate, out GeoCoordinate parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public GeoCoordinate Create(ReadOnlySpan<char> alternate) => GeoCoordinate.Parse(alternate);
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidLatitude(double v) => !double.IsNaN(v) && v >= -90.0 && v <= 90.0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidLongitude(double v) => !double.IsNaN(v) && v >= -180.0 && v <= 180.0;

    /// <summary>Deconstructs the coordinate into its latitude and longitude components.</summary>
    /// <param name="lat">When this method returns, contains the latitude in degrees.</param>
    /// <param name="lng">When this method returns, contains the longitude in degrees.</param>
    public void Deconstruct(out double lat, out double lng) { lat = this.Latitude; lng = this.Longitude; }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Latitude, this.Longitude);
}

#pragma warning disable WP_GEO_COORDINATE
/// <summary>
/// JSON converter for <see cref="GeoCoordinate"/>.
/// </summary>
public sealed class GeoCoordinateJsonConverter : JsonConverter<GeoCoordinate> {
    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, GeoCoordinate value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteNumber("lat", value.Latitude);
        writer.WriteNumber("lng", value.Longitude);
        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public override GeoCoordinate ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? propName = reader.GetString();
        if(propName is not null && GeoCoordinate.TryParse(propName, out GeoCoordinate result)) {
            return result;
        }
        throw new JsonException($"Invalid property name format for GeoCoordinate: '{propName}'.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, GeoCoordinate value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }
}
#pragma warning restore WP_GEO_COORDINATE