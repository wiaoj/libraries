using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static Wiaoj.WellKnown.OAuthProtectedResourceMetadata;

namespace Wiaoj.WellKnown;

/// <summary>
/// An RFC 8414 OAuth 2.0 authorization server metadata document.
/// </summary>
/// <remarks>
/// Built from <see cref="OAuthAuthorizationServerOptions"/> by <see cref="FromOptions"/>. Every optional parameter is
/// nullable and omitted when null, and <see cref="FromOptions"/> maps empty lists and <see langword="false"/> to null,
/// so a zero-element parameter never reaches the document (§3.2).
/// </remarks>
public sealed record OAuthAuthorizationServerMetadata {
    /// <summary>Gets the issuer identifier.</summary>
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    /// <summary>Gets the authorization endpoint URL.</summary>
    [JsonPropertyName("authorization_endpoint")]
    public string? AuthorizationEndpoint { get; init; }

    /// <summary>Gets the token endpoint URL.</summary>
    [JsonPropertyName("token_endpoint")]
    public string? TokenEndpoint { get; init; }

    /// <summary>Gets the JSON Web Key Set URL.</summary>
    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; init; }

    /// <summary>Gets the dynamic client registration endpoint URL.</summary>
    [JsonPropertyName("registration_endpoint")]
    public string? RegistrationEndpoint { get; init; }

    /// <summary>Gets the supported scope values.</summary>
    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string>? ScopesSupported { get; init; }

    /// <summary>Gets the supported response types.</summary>
    [JsonPropertyName("response_types_supported")]
    public required IReadOnlyList<string> ResponseTypesSupported { get; init; }

    /// <summary>Gets the supported response modes.</summary>
    [JsonPropertyName("response_modes_supported")]
    public IReadOnlyList<string>? ResponseModesSupported { get; init; }

    /// <summary>Gets the supported grant types.</summary>
    [JsonPropertyName("grant_types_supported")]
    public IReadOnlyList<string>? GrantTypesSupported { get; init; }

    /// <summary>Gets the token endpoint's client authentication methods.</summary>
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public IReadOnlyList<string>? TokenEndpointAuthMethodsSupported { get; init; }

    /// <summary>Gets the token endpoint's client authentication JWT algorithms.</summary>
    [JsonPropertyName("token_endpoint_auth_signing_alg_values_supported")]
    public IReadOnlyList<string>? TokenEndpointAuthSigningAlgValuesSupported { get; init; }

    /// <summary>Gets the developer documentation URL.</summary>
    [JsonPropertyName("service_documentation")]
    public string? ServiceDocumentation { get; init; }

    /// <summary>Gets the user interface language tags.</summary>
    [JsonPropertyName("ui_locales_supported")]
    public IReadOnlyList<string>? UiLocalesSupported { get; init; }

    /// <summary>Gets the data usage policy URL.</summary>
    [JsonPropertyName("op_policy_uri")]
    public string? OpPolicyUri { get; init; }

    /// <summary>Gets the terms of service URL.</summary>
    [JsonPropertyName("op_tos_uri")]
    public string? OpTosUri { get; init; }

    /// <summary>Gets the revocation endpoint URL.</summary>
    [JsonPropertyName("revocation_endpoint")]
    public string? RevocationEndpoint { get; init; }

    /// <summary>Gets the revocation endpoint's client authentication methods.</summary>
    [JsonPropertyName("revocation_endpoint_auth_methods_supported")]
    public IReadOnlyList<string>? RevocationEndpointAuthMethodsSupported { get; init; }

    /// <summary>Gets the revocation endpoint's client authentication JWT algorithms.</summary>
    [JsonPropertyName("revocation_endpoint_auth_signing_alg_values_supported")]
    public IReadOnlyList<string>? RevocationEndpointAuthSigningAlgValuesSupported { get; init; }

    /// <summary>Gets the introspection endpoint URL.</summary>
    [JsonPropertyName("introspection_endpoint")]
    public string? IntrospectionEndpoint { get; init; }

    /// <summary>Gets the introspection endpoint's client authentication methods.</summary>
    [JsonPropertyName("introspection_endpoint_auth_methods_supported")]
    public IReadOnlyList<string>? IntrospectionEndpointAuthMethodsSupported { get; init; }

    /// <summary>Gets the introspection endpoint's client authentication JWT algorithms.</summary>
    [JsonPropertyName("introspection_endpoint_auth_signing_alg_values_supported")]
    public IReadOnlyList<string>? IntrospectionEndpointAuthSigningAlgValuesSupported { get; init; }

    /// <summary>Gets the supported PKCE code challenge methods.</summary>
    [JsonPropertyName("code_challenge_methods_supported")]
    public IReadOnlyList<string>? CodeChallengeMethodsSupported { get; init; }

    /// <summary>Gets the RFC 8628 device authorization endpoint URL.</summary>
    [JsonPropertyName("device_authorization_endpoint")]
    public string? DeviceAuthorizationEndpoint { get; init; }

    /// <summary>Gets the RFC 9126 pushed authorization request endpoint URL.</summary>
    [JsonPropertyName("pushed_authorization_request_endpoint")]
    public string? PushedAuthorizationRequestEndpoint { get; init; }

    /// <summary>Gets whether only pushed authorization requests are accepted; null when not.</summary>
    [JsonPropertyName("require_pushed_authorization_requests")]
    public bool? RequirePushedAuthorizationRequests { get; init; }

    /// <summary>Gets whether the authorization response carries <c>iss</c>; null when not.</summary>
    [JsonPropertyName("authorization_response_iss_parameter_supported")]
    public bool? AuthorizationResponseIssParameterSupported { get; init; }

    /// <summary>Gets the JWS algorithms accepted for DPoP proofs.</summary>
    [JsonPropertyName("dpop_signing_alg_values_supported")]
    public IReadOnlyList<string>? DpopSigningAlgValuesSupported { get; init; }

    /// <summary>Gets whether mutual-TLS certificate-bound access tokens are supported; null when not.</summary>
    [JsonPropertyName("tls_client_certificate_bound_access_tokens")]
    public bool? TlsClientCertificateBoundAccessTokens { get; init; }

    /// <summary>Gets the protected resources that can be used with the server.</summary>
    [JsonPropertyName("protected_resources")]
    public IReadOnlyList<string>? ProtectedResources { get; init; }

    /// <summary>Gets the parameters published beside the typed ones, written as top-level members; null when there are none.</summary>
    /// <remarks>Merged into the document by <see cref="ToUtf8Json"/>, not through extension data, which source-generated serialization writes incorrectly for nested values.</remarks>
    [JsonIgnore]
    public JsonObject? AdditionalParameters { get; init; }

    /// <summary>
    /// The parameter names this type publishes from a typed option, plus <c>signed_metadata</c>. An additional parameter
    /// may not reuse them; every other registered name — <c>userinfo_endpoint</c>, <c>mtls_endpoint_aliases</c> — may be
    /// published as an additional parameter.
    /// </summary>
    public static IReadOnlySet<string> StandardParameterNames { get; } = new HashSet<string>(StringComparer.Ordinal) {
        "issuer", "authorization_endpoint", "token_endpoint", "jwks_uri", "registration_endpoint", "scopes_supported",
        "response_types_supported", "response_modes_supported", "grant_types_supported",
        "token_endpoint_auth_methods_supported", "token_endpoint_auth_signing_alg_values_supported",
        "service_documentation", "ui_locales_supported", "op_policy_uri", "op_tos_uri", "revocation_endpoint",
        "revocation_endpoint_auth_methods_supported", "revocation_endpoint_auth_signing_alg_values_supported",
        "introspection_endpoint", "introspection_endpoint_auth_methods_supported",
        "introspection_endpoint_auth_signing_alg_values_supported", "code_challenge_methods_supported",
        "signed_metadata", "device_authorization_endpoint", "pushed_authorization_request_endpoint",
        "require_pushed_authorization_requests", "authorization_response_iss_parameter_supported",
        "dpop_signing_alg_values_supported", "tls_client_certificate_bound_access_tokens", "protected_resources"
    };

    /// <summary>
    /// Builds the document for <paramref name="options"/>, omitting every zero-valued parameter.
    /// </summary>
    /// <param name="options">Validated options for one authorization server.</param>
    /// <returns>The metadata document.</returns>
    /// <exception cref="InvalidOperationException"><see cref="OAuthAuthorizationServerOptions.Issuer"/> or <see cref="OAuthAuthorizationServerOptions.ResponseTypesSupported"/> is not set.</exception>
    public static OAuthAuthorizationServerMetadata FromOptions(OAuthAuthorizationServerOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        return new OAuthAuthorizationServerMetadata {
            Issuer = options.Issuer ?? throw new InvalidOperationException("The authorization server has no Issuer."),
            AuthorizationEndpoint = NullIfBlank(options.AuthorizationEndpoint),
            TokenEndpoint = NullIfBlank(options.TokenEndpoint),
            JwksUri = NullIfBlank(options.JwksUri),
            RegistrationEndpoint = NullIfBlank(options.RegistrationEndpoint),
            ScopesSupported = NullIfEmpty(options.Scopes.Order(StringComparer.Ordinal)),
            ResponseTypesSupported = NullIfEmpty(options.ResponseTypesSupported)
                ?? throw new InvalidOperationException("The authorization server has no ResponseTypesSupported."),
            ResponseModesSupported = NullIfEmpty(options.ResponseModesSupported),
            GrantTypesSupported = NullIfEmpty(options.GrantTypesSupported),
            TokenEndpointAuthMethodsSupported = NullIfEmpty(options.TokenEndpointAuthMethodsSupported),
            TokenEndpointAuthSigningAlgValuesSupported = NullIfEmpty(options.TokenEndpointAuthSigningAlgValuesSupported),
            ServiceDocumentation = NullIfBlank(options.ServiceDocumentation),
            UiLocalesSupported = NullIfEmpty(options.UiLocalesSupported),
            OpPolicyUri = NullIfBlank(options.OpPolicyUri),
            OpTosUri = NullIfBlank(options.OpTosUri),
            RevocationEndpoint = NullIfBlank(options.RevocationEndpoint),
            RevocationEndpointAuthMethodsSupported = NullIfEmpty(options.RevocationEndpointAuthMethodsSupported),
            RevocationEndpointAuthSigningAlgValuesSupported = NullIfEmpty(options.RevocationEndpointAuthSigningAlgValuesSupported),
            IntrospectionEndpoint = NullIfBlank(options.IntrospectionEndpoint),
            IntrospectionEndpointAuthMethodsSupported = NullIfEmpty(options.IntrospectionEndpointAuthMethodsSupported),
            IntrospectionEndpointAuthSigningAlgValuesSupported = NullIfEmpty(options.IntrospectionEndpointAuthSigningAlgValuesSupported),
            CodeChallengeMethodsSupported = NullIfEmpty(options.CodeChallengeMethodsSupported),
            DeviceAuthorizationEndpoint = NullIfBlank(options.DeviceAuthorizationEndpoint),
            PushedAuthorizationRequestEndpoint = NullIfBlank(options.PushedAuthorizationRequestEndpoint),
            RequirePushedAuthorizationRequests = options.RequirePushedAuthorizationRequests ? true : null,
            AuthorizationResponseIssParameterSupported = options.AuthorizationResponseIssParameterSupported ? true : null,
            DpopSigningAlgValuesSupported = NullIfEmpty(options.DpopSigningAlgValuesSupported),
            TlsClientCertificateBoundAccessTokens = options.TlsClientCertificateBoundAccessTokens ? true : null,
            ProtectedResources = NullIfEmpty(options.ProtectedResources),
            AdditionalParameters = WellKnownDocument.CopyAdditionalParameters(options.AdditionalParameters)
        };
    }

    /// <summary>
    /// Writes the document as UTF-8 JSON: the typed parameters, followed by the additional ones as top-level members.
    /// </summary>
    /// <returns>The JSON bytes.</returns>
    public byte[] ToUtf8Json() {
        return WellKnownDocument.ToUtf8Json(
            JsonSerializer.SerializeToNode(this, WellKnownJsonContext.Default.OAuthAuthorizationServerMetadata)!,
            this.AdditionalParameters);
    }
}
