using System.Text.Json.Serialization;

namespace Wiaoj.WellKnown;

/// <summary>
/// An RFC 9728 OAuth 2.0 protected resource metadata document.
/// </summary>
/// <remarks>
/// Built from <see cref="OAuthProtectedResourceOptions"/> by <see cref="FromOptions"/>. Every optional parameter is
/// nullable and omitted when null, and <see cref="FromOptions"/> maps empty lists and <see langword="false"/> to null,
/// so a zero-valued parameter never reaches the document (§3.2).
/// </remarks>
public sealed record OAuthProtectedResourceMetadata {
    /// <summary>Gets the protected resource's identifier.</summary>
    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    /// <summary>Gets the authorization servers' issuer identifiers.</summary>
    [JsonPropertyName("authorization_servers")]
    public IReadOnlyList<string>? AuthorizationServers { get; init; }

    /// <summary>Gets the URL of the resource's JSON Web Key Set.</summary>
    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; init; }

    /// <summary>Gets the scopes used to request access to the resource.</summary>
    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string>? ScopesSupported { get; init; }

    /// <summary>Gets the supported ways of presenting an access token.</summary>
    [JsonPropertyName("bearer_methods_supported")]
    public IReadOnlyList<string>? BearerMethodsSupported { get; init; }

    /// <summary>Gets the JWS algorithms the resource signs responses with.</summary>
    [JsonPropertyName("resource_signing_alg_values_supported")]
    public IReadOnlyList<string>? ResourceSigningAlgValuesSupported { get; init; }

    /// <summary>Gets the human-readable resource name.</summary>
    [JsonPropertyName("resource_name")]
    public string? ResourceName { get; init; }

    /// <summary>Gets the URL of developer documentation.</summary>
    [JsonPropertyName("resource_documentation")]
    public string? ResourceDocumentation { get; init; }

    /// <summary>Gets the URL of the data usage policy.</summary>
    [JsonPropertyName("resource_policy_uri")]
    public string? ResourcePolicyUri { get; init; }

    /// <summary>Gets the URL of the terms of service.</summary>
    [JsonPropertyName("resource_tos_uri")]
    public string? ResourceTosUri { get; init; }

    /// <summary>Gets whether mutual-TLS certificate-bound access tokens are supported; null when not.</summary>
    [JsonPropertyName("tls_client_certificate_bound_access_tokens")]
    public bool? TlsClientCertificateBoundAccessTokens { get; init; }

    /// <summary>Gets the supported authorization details types.</summary>
    [JsonPropertyName("authorization_details_types_supported")]
    public IReadOnlyList<string>? AuthorizationDetailsTypesSupported { get; init; }

    /// <summary>Gets the JWS algorithms accepted for DPoP proofs.</summary>
    [JsonPropertyName("dpop_signing_alg_values_supported")]
    public IReadOnlyList<string>? DpopSigningAlgValuesSupported { get; init; }

    /// <summary>Gets whether DPoP-bound access tokens are always required; null when not.</summary>
    [JsonPropertyName("dpop_bound_access_tokens_required")]
    public bool? DpopBoundAccessTokensRequired { get; init; }

    /// <summary>
    /// Builds the document for <paramref name="options"/>, omitting every zero-valued parameter.
    /// </summary>
    /// <param name="options">Validated options for one protected resource.</param>
    /// <returns>The metadata document.</returns>
    /// <exception cref="InvalidOperationException"><see cref="OAuthProtectedResourceOptions.Resource"/> is not set.</exception>
    public static OAuthProtectedResourceMetadata FromOptions(OAuthProtectedResourceOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        return new OAuthProtectedResourceMetadata {
            Resource = options.Resource ?? throw new InvalidOperationException("The protected resource has no Resource identifier."),
            AuthorizationServers = NullIfEmpty(options.AuthorizationServers),
            JwksUri = NullIfBlank(options.JwksUri),
            ScopesSupported = NullIfEmpty(options.Scopes.Order(StringComparer.Ordinal)),
            BearerMethodsSupported = NullIfEmpty(options.BearerMethodsSupported),
            ResourceSigningAlgValuesSupported = NullIfEmpty(options.ResourceSigningAlgValuesSupported),
            ResourceName = NullIfBlank(options.ResourceName),
            ResourceDocumentation = NullIfBlank(options.ResourceDocumentation),
            ResourcePolicyUri = NullIfBlank(options.ResourcePolicyUri),
            ResourceTosUri = NullIfBlank(options.ResourceTosUri),
            TlsClientCertificateBoundAccessTokens = options.TlsClientCertificateBoundAccessTokens ? true : null,
            AuthorizationDetailsTypesSupported = NullIfEmpty(options.AuthorizationDetailsTypesSupported),
            DpopSigningAlgValuesSupported = NullIfEmpty(options.DpopSigningAlgValuesSupported),
            DpopBoundAccessTokensRequired = options.DpopBoundAccessTokensRequired ? true : null
        };
    }

    private static IReadOnlyList<string>? NullIfEmpty(IEnumerable<string> values) {
        string[] distinct = [.. values.Distinct(StringComparer.Ordinal)];
        return distinct.Length == 0 ? null : distinct;
    }

    private static string? NullIfBlank(string? value) {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
