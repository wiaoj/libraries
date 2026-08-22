using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.JsonConverters;

/// <summary>
/// A custom <see cref="JsonConverter{T}"/> for serializing and deserializing <see cref="SemVer"/> instances.
/// </summary>
public sealed class SemVerJsonConverter : JsonConverter<SemVer> {
    /// <inheritdoc/>
    public override SemVer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType == JsonTokenType.String) {
            if(!reader.ValueIsEscaped && !reader.HasValueSequence) {
                if(SemVer.TryParse(reader.ValueSpan, out SemVer result)) {
                    return result;
                }
            }

            string? str = reader.GetString();
            if(str is not null && SemVer.TryParse(str, out SemVer parsed)) {
                return parsed;
            }

            throw new JsonException($"Unable to parse '{reader.GetString()}' as a valid SemVer.");
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for SemVer.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SemVer value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString());
    }

    /// <inheritdoc/>
    public override SemVer ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        string? propName = reader.GetString();
        if(propName is not null && SemVer.TryParse(propName, out SemVer parsed)) {
            return parsed;
        }

        throw new JsonException($"Invalid property name format for SemVer: '{propName}'.");
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, SemVer value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.ToString());
    }
}
