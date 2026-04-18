using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Primitives.JsonConverters; 
/// <summary>
/// Converts a <see cref="Crc32Hash"/> to and from its JSON string representation (Hexadecimal).
/// </summary>
public sealed class Crc32HashJsonConverter : JsonConverter<Crc32Hash> {

    /// <summary>
    /// Reads and converts the JSON string to a <see cref="Crc32Hash"/>.
    /// </summary>
    public override Crc32Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if(reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected string token for Crc32Hash, but got {reader.TokenType}.");
        }

        string? value = reader.GetString();

        if(string.IsNullOrEmpty(value)) {
            throw new JsonException("Cannot parse an empty string as a Crc32Hash.");
        }

        if(!Crc32Hash.TryParse(value, out Crc32Hash result)) {
            throw new JsonException($"The string '{value}' is not a valid 4-byte hex representation of a Crc32Hash.");
        }

        return result;
    }

    /// <summary>
    /// Writes the <see cref="Crc32Hash"/> as a JSON string using zero-allocation formatting.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Crc32Hash value, JsonSerializerOptions options) {
        Span<char> buffer = stackalloc char[Crc32Hash.HashSizeInBytes * 2];

        if(value.TryFormat(buffer, out int charsWritten, lowerCase: false)) {
            writer.WriteStringValue(buffer[..charsWritten]);
        }
        else {
            writer.WriteStringValue(value.ToString());
        }
    }
}