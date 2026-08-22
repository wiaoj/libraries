using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.Cryptography.Asymmetric;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(Jwk))]
[JsonSerializable(typeof(JwksDocument))]
internal sealed partial class JwkJsonSerializerContext : JsonSerializerContext {

    public static readonly JwkJsonSerializerContext Compact = new(new JsonSerializerOptions {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    });

    public static readonly JwkJsonSerializerContext Indented = new(new JsonSerializerOptions {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    });
}