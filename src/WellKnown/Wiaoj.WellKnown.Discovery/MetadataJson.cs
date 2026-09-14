using System.Text.Json;

namespace Wiaoj.WellKnown.Discovery;

/// <summary>
/// Reads known parameters from a metadata document. Unknown parameters are ignored (RFC 9728 §3.2); a known parameter of
/// the wrong type makes the document unusable rather than silently absent.
/// </summary>
internal static class MetadataJson {
    public static string RequiredString(JsonElement document, string name, Uri source) {
        return OptionalString(document, name, source)
            ?? throw Invalid(source, $"has no '{name}', which is required");
    }

    public static string? OptionalString(JsonElement document, string name, Uri source) {
        if(!document.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw Invalid(source, $"has '{name}' of type {value.ValueKind}, not a string");
    }

    public static IReadOnlyList<string>? OptionalStrings(JsonElement document, string name, Uri source) {
        if(!document.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) {
            return null;
        }

        if(value.ValueKind != JsonValueKind.Array) {
            throw Invalid(source, $"has '{name}' of type {value.ValueKind}, not an array");
        }

        List<string> values = new(value.GetArrayLength());
        foreach(JsonElement item in value.EnumerateArray()) {
            values.Add(item.ValueKind == JsonValueKind.String
                ? item.GetString()!
                : throw Invalid(source, $"has an element of '{name}' of type {item.ValueKind}, not a string"));
        }

        return values;
    }

    public static bool OptionalBoolean(JsonElement document, string name, Uri source) {
        if(!document.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) {
            return false;
        }

        return value.ValueKind switch {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Invalid(source, $"has '{name}' of type {value.ValueKind}, not a boolean")
        };
    }

    private static OAuthDiscoveryException Invalid(Uri source, string problem) {
        return new OAuthDiscoveryException(OAuthDiscoveryFailure.InvalidDocument, $"The metadata document at '{source}' {problem}.");
    }
}
