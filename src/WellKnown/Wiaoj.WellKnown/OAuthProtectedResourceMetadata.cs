using System.Text.Json.Serialization;

namespace Wiaoj.WellKnown;

/// <summary>
/// RFC 9728 OAuth 2.0 Protected Resource Metadata standardı.
/// </summary>
public sealed record OAuthProtectedResourceMetadata {
    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    [JsonPropertyName("authorization_servers")]
    public required IReadOnlyList<string> AuthorizationServers { get; init; }

    [JsonPropertyName("scopes_supported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ScopesSupported { get; init; }

    [JsonPropertyName("bearer_methods_supported")]
    public IReadOnlyList<string> BearerMethodsSupported { get; init; } = ["header"];

    [JsonPropertyName("resource_documentation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceDocumentation { get; init; }
}