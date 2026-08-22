using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Cryptography;

namespace Wiaoj.Primitives.JsonConverters;

/// <summary>
/// Serializes a <see cref="PemString"/> as its raw PEM text (including the <c>-----BEGIN/END-----</c> boundaries).
/// </summary>
/// <remarks>
/// Unlike <see cref="PemString.ToString()"/>, which returns a log-safe sentinel, this converter writes
/// the actual PEM content — JSON payloads (e.g. JWKS-adjacent documents, config files) are expected to
/// carry the real value. Only use this converter for genuinely public PEM data.
/// </remarks>
public sealed class PemStringJsonConverter : JsonConverter<PemString> {
    /// <inheritdoc/>
    public override PemString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? value = reader.GetString();
        return string.IsNullOrEmpty(value) ? PemString.Empty : PemString.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PemString value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}