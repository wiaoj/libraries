using System.Text.Json.Nodes;

namespace Wiaoj.WellKnown;

/// <summary>
/// Describes one OAuth 2.0 authorization server, as published in its RFC 8414 metadata document.
/// </summary>
/// <remarks>
/// <para>
/// Each property maps to one metadata parameter — of RFC 8414 §2, or of the extension named on it. An unset parameter
/// — null, an empty list, or <see langword="false"/> — is left out of the document; §3.2 requires that for empty arrays.
/// </para>
/// <para>
/// Three parameters are required. <see cref="Issuer"/> and <see cref="ResponseTypesSupported"/> always;
/// <see cref="AuthorizationEndpoint"/> unless no supported grant type uses it, and <see cref="TokenEndpoint"/> unless
/// only the implicit grant is supported. Both conditions read <see cref="GrantTypesSupported"/>, which clients take to
/// be <c>authorization_code</c> and <c>implicit</c> when it is left empty.
/// </para>
/// <para>
/// The options are validated when the application starts, and every problem is reported together.
/// </para>
/// </remarks>
public sealed class OAuthAuthorizationServerOptions {
    /// <summary>
    /// Gets or sets the authorization server's issuer identifier — <c>issuer</c>. Required.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An absolute <c>https</c> URL with no query and no fragment, such as <c>https://auth.example.com</c> or
    /// <c>https://auth.example.com/tenant1</c>. A client refuses the document unless this is identical to the issuer it
    /// derived the metadata URL from (§3.3), and it is what protects clients from mix-up attacks — so it is the public
    /// address, and never taken from a request. For local development, <c>http</c> is accepted on a loopback host.
    /// </para>
    /// <para>
    /// The document's URL is derived from it, with any terminating slash of the path removed (§3.1):
    /// <c>https://auth.example.com/tenant1</c> is served at <c>/.well-known/oauth-authorization-server/tenant1</c>.
    /// </para>
    /// </remarks>
    public string? Issuer { get; set; }

    /// <summary>Gets or sets the URL of the authorization endpoint — <c>authorization_endpoint</c>.</summary>
    /// <remarks>Required unless no supported grant type uses the authorization endpoint.</remarks>
    public string? AuthorizationEndpoint { get; set; }

    /// <summary>Gets or sets the URL of the token endpoint — <c>token_endpoint</c>.</summary>
    /// <remarks>Required unless only the implicit grant is supported.</remarks>
    public string? TokenEndpoint { get; set; }

    /// <summary>Gets or sets the URL of the server's JSON Web Key Set — <c>jwks_uri</c>. Must use <c>https</c>.</summary>
    public string? JwksUri { get; set; }

    /// <summary>Gets or sets the URL of the RFC 7591 dynamic client registration endpoint — <c>registration_endpoint</c>.</summary>
    public string? RegistrationEndpoint { get; set; }

    /// <summary>Gets the scope values the server supports — <c>scopes_supported</c>. RFC 8414 recommends publishing them.</summary>
    /// <remarks>Written de-duplicated, in ordinal order. A server may leave some supported scopes out.</remarks>
    public HashSet<string> Scopes { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the <c>response_type</c> values the server supports — <c>response_types_supported</c>. Required.</summary>
    public List<string> ResponseTypesSupported { get; } = [];

    /// <summary>Gets the <c>response_mode</c> values the server supports — <c>response_modes_supported</c>. Clients assume <c>query</c> and <c>fragment</c> when empty.</summary>
    public List<string> ResponseModesSupported { get; } = [];

    /// <summary>Gets the grant types the server supports — <c>grant_types_supported</c>. Clients assume <c>authorization_code</c> and <c>implicit</c> when empty.</summary>
    public List<string> GrantTypesSupported { get; } = [];

    /// <summary>Gets the client authentication methods of the token endpoint — <c>token_endpoint_auth_methods_supported</c>. Clients assume <c>client_secret_basic</c> when empty.</summary>
    public List<string> TokenEndpointAuthMethodsSupported { get; } = [];

    /// <summary>Gets the JWS algorithms for client authentication JWTs at the token endpoint — <c>token_endpoint_auth_signing_alg_values_supported</c>.</summary>
    /// <remarks>Required when <c>private_key_jwt</c> or <c>client_secret_jwt</c> is supported. <c>none</c> is not allowed.</remarks>
    public List<string> TokenEndpointAuthSigningAlgValuesSupported { get; } = [];

    /// <summary>Gets or sets the URL of human-readable developer documentation — <c>service_documentation</c>.</summary>
    public string? ServiceDocumentation { get; set; }

    /// <summary>Gets the BCP 47 language tags of the user interface — <c>ui_locales_supported</c>.</summary>
    public List<string> UiLocalesSupported { get; } = [];

    /// <summary>Gets or sets the URL of the server's policy on how clients may use its data — <c>op_policy_uri</c>.</summary>
    public string? OpPolicyUri { get; set; }

    /// <summary>Gets or sets the URL of the server's terms of service — <c>op_tos_uri</c>.</summary>
    public string? OpTosUri { get; set; }

    /// <summary>Gets or sets the URL of the RFC 7009 revocation endpoint — <c>revocation_endpoint</c>.</summary>
    public string? RevocationEndpoint { get; set; }

    /// <summary>Gets the client authentication methods of the revocation endpoint — <c>revocation_endpoint_auth_methods_supported</c>.</summary>
    public List<string> RevocationEndpointAuthMethodsSupported { get; } = [];

    /// <summary>Gets the JWS algorithms for client authentication JWTs at the revocation endpoint — <c>revocation_endpoint_auth_signing_alg_values_supported</c>.</summary>
    /// <remarks>Required when <c>private_key_jwt</c> or <c>client_secret_jwt</c> is supported there. <c>none</c> is not allowed.</remarks>
    public List<string> RevocationEndpointAuthSigningAlgValuesSupported { get; } = [];

    /// <summary>Gets or sets the URL of the RFC 7662 introspection endpoint — <c>introspection_endpoint</c>.</summary>
    public string? IntrospectionEndpoint { get; set; }

    /// <summary>Gets the client authentication methods of the introspection endpoint — <c>introspection_endpoint_auth_methods_supported</c>.</summary>
    public List<string> IntrospectionEndpointAuthMethodsSupported { get; } = [];

    /// <summary>Gets the JWS algorithms for client authentication JWTs at the introspection endpoint — <c>introspection_endpoint_auth_signing_alg_values_supported</c>.</summary>
    /// <remarks>Required when <c>private_key_jwt</c> or <c>client_secret_jwt</c> is supported there. <c>none</c> is not allowed.</remarks>
    public List<string> IntrospectionEndpointAuthSigningAlgValuesSupported { get; } = [];

    /// <summary>Gets the RFC 7636 PKCE code challenge methods — <c>code_challenge_methods_supported</c>. Clients assume PKCE is unsupported when empty.</summary>
    public List<string> CodeChallengeMethodsSupported { get; } = [];

    /// <summary>Gets or sets the URL of the RFC 8628 device authorization endpoint — <c>device_authorization_endpoint</c>.</summary>
    /// <remarks>What a command-line tool or other input-constrained client starts the device flow at.</remarks>
    public string? DeviceAuthorizationEndpoint { get; set; }

    /// <summary>Gets or sets the URL of the RFC 9126 pushed authorization request endpoint — <c>pushed_authorization_request_endpoint</c>.</summary>
    public string? PushedAuthorizationRequestEndpoint { get; set; }

    /// <summary>Gets or sets whether authorization requests are accepted only through PAR — <c>require_pushed_authorization_requests</c> (RFC 9126).</summary>
    public bool RequirePushedAuthorizationRequests { get; set; }

    /// <summary>Gets or sets whether the authorization response carries <c>iss</c> — <c>authorization_response_iss_parameter_supported</c> (RFC 9207).</summary>
    public bool AuthorizationResponseIssParameterSupported { get; set; }

    /// <summary>Gets the JWS algorithms accepted for DPoP proof JWTs — <c>dpop_signing_alg_values_supported</c> (RFC 9449). <c>none</c> is not allowed.</summary>
    public List<string> DpopSigningAlgValuesSupported { get; } = [];

    /// <summary>Gets or sets whether mutual-TLS certificate-bound access tokens are supported — <c>tls_client_certificate_bound_access_tokens</c> (RFC 8705).</summary>
    public bool TlsClientCertificateBoundAccessTokens { get; set; }

    /// <summary>Gets the resource identifiers of the protected resources that can be used with this server — <c>protected_resources</c> (RFC 9728 §4).</summary>
    /// <remarks>Optional, and a server may leave some out. Clients can cross-check it against a resource's <c>authorization_servers</c>.</remarks>
    public List<string> ProtectedResources { get; } = [];

    /// <summary>
    /// Gets parameters published beside the typed ones — a registered parameter without its own option, such as
    /// <c>userinfo_endpoint</c> or <c>mtls_endpoint_aliases</c>, or one of the organisation's own.
    /// </summary>
    /// <remarks>
    /// Values are JSON nodes, so any JSON can be written without reflection. A name that has a typed option, or
    /// <c>signed_metadata</c>, is refused at startup: it would publish something the validation never saw.
    /// </remarks>
    public Dictionary<string, JsonNode?> AdditionalParameters { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets how long clients may cache the document, sent as <c>Cache-Control: public, max-age</c>. Defaults to
    /// one day; <see cref="TimeSpan.Zero"/> sends <c>no-cache</c>.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}
