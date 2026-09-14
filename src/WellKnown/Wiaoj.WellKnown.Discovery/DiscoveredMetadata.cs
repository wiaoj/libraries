using System.Text.Json;
using static Wiaoj.WellKnown.Discovery.MetadataJson;

namespace Wiaoj.WellKnown.Discovery;

/// <summary>
/// A validated RFC 9728 protected resource metadata document.
/// </summary>
/// <remarks>
/// Only returned once <see cref="Resource"/> has been checked against the identifier it was looked up for. Parameters
/// without a property here are in <see cref="Json"/>.
/// </remarks>
public sealed class ProtectedResourceMetadataDocument {
    private ProtectedResourceMetadataDocument(Uri source, JsonElement json) {
        this.Source = source;
        this.Json = json;
        this.Resource = RequiredString(json, "resource", source);
        this.AuthorizationServers = OptionalStrings(json, "authorization_servers", source) ?? [];
        this.JwksUri = OptionalString(json, "jwks_uri", source);
        this.ScopesSupported = OptionalStrings(json, "scopes_supported", source) ?? [];
        this.BearerMethodsSupported = OptionalStrings(json, "bearer_methods_supported", source) ?? [];
        this.ResourceName = OptionalString(json, "resource_name", source);
        this.DpopSigningAlgValuesSupported = OptionalStrings(json, "dpop_signing_alg_values_supported", source) ?? [];
        this.DpopBoundAccessTokensRequired = OptionalBoolean(json, "dpop_bound_access_tokens_required", source);
        this.TlsClientCertificateBoundAccessTokens = OptionalBoolean(json, "tls_client_certificate_bound_access_tokens", source);
    }

    /// <summary>Gets the URL the document was fetched from.</summary>
    public Uri Source { get; }

    /// <summary>Gets the whole document, for parameters without a property of their own.</summary>
    public JsonElement Json { get; }

    /// <summary>Gets the resource identifier — <c>resource</c>.</summary>
    public string Resource { get; }

    /// <summary>Gets the issuers of the authorization servers for this resource — <c>authorization_servers</c>; empty when none are listed.</summary>
    public IReadOnlyList<string> AuthorizationServers { get; }

    /// <summary>Gets the resource's JSON Web Key Set URL — <c>jwks_uri</c>.</summary>
    public string? JwksUri { get; }

    /// <summary>Gets the scopes used to request access — <c>scopes_supported</c>; empty when not published.</summary>
    public IReadOnlyList<string> ScopesSupported { get; }

    /// <summary>Gets the supported ways of presenting a token — <c>bearer_methods_supported</c>; empty when not published.</summary>
    public IReadOnlyList<string> BearerMethodsSupported { get; }

    /// <summary>Gets the human-readable resource name — <c>resource_name</c>.</summary>
    public string? ResourceName { get; }

    /// <summary>Gets the algorithms accepted for DPoP proofs — <c>dpop_signing_alg_values_supported</c>; empty when not published.</summary>
    public IReadOnlyList<string> DpopSigningAlgValuesSupported { get; }

    /// <summary>Gets whether DPoP-bound tokens are always required — <c>dpop_bound_access_tokens_required</c>.</summary>
    public bool DpopBoundAccessTokensRequired { get; }

    /// <summary>Gets whether mutual-TLS certificate-bound tokens are supported — <c>tls_client_certificate_bound_access_tokens</c>.</summary>
    public bool TlsClientCertificateBoundAccessTokens { get; }

    internal static ProtectedResourceMetadataDocument Parse(Uri source, JsonElement json) => new(source, json);
}

/// <summary>
/// A validated RFC 8414 authorization server metadata document.
/// </summary>
/// <remarks>
/// Only returned once <see cref="Issuer"/> has been checked against the issuer it was looked up for. Parameters without
/// a property here — <c>userinfo_endpoint</c>, <c>mtls_endpoint_aliases</c> — are in <see cref="Json"/>.
/// </remarks>
public sealed class AuthorizationServerMetadataDocument {
    private AuthorizationServerMetadataDocument(Uri source, JsonElement json) {
        this.Source = source;
        this.Json = json;
        this.Issuer = RequiredString(json, "issuer", source);
        this.AuthorizationEndpoint = OptionalString(json, "authorization_endpoint", source);
        this.TokenEndpoint = OptionalString(json, "token_endpoint", source);
        this.JwksUri = OptionalString(json, "jwks_uri", source);
        this.RegistrationEndpoint = OptionalString(json, "registration_endpoint", source);
        this.ScopesSupported = OptionalStrings(json, "scopes_supported", source) ?? [];
        this.ResponseTypesSupported = OptionalStrings(json, "response_types_supported", source) ?? [];
        this.GrantTypesSupported = OptionalStrings(json, "grant_types_supported", source) ?? ["authorization_code", "implicit"];
        this.TokenEndpointAuthMethodsSupported = OptionalStrings(json, "token_endpoint_auth_methods_supported", source) ?? ["client_secret_basic"];
        this.CodeChallengeMethodsSupported = OptionalStrings(json, "code_challenge_methods_supported", source) ?? [];
        this.RevocationEndpoint = OptionalString(json, "revocation_endpoint", source);
        this.IntrospectionEndpoint = OptionalString(json, "introspection_endpoint", source);
        this.DeviceAuthorizationEndpoint = OptionalString(json, "device_authorization_endpoint", source);
        this.PushedAuthorizationRequestEndpoint = OptionalString(json, "pushed_authorization_request_endpoint", source);
        this.DpopSigningAlgValuesSupported = OptionalStrings(json, "dpop_signing_alg_values_supported", source) ?? [];
        this.ProtectedResources = OptionalStrings(json, "protected_resources", source);
    }

    /// <summary>Gets the URL the document was fetched from.</summary>
    public Uri Source { get; }

    /// <summary>Gets the whole document, for parameters without a property of their own.</summary>
    public JsonElement Json { get; }

    /// <summary>Gets the issuer identifier — <c>issuer</c>.</summary>
    public string Issuer { get; }

    /// <summary>Gets the authorization endpoint — <c>authorization_endpoint</c>.</summary>
    public string? AuthorizationEndpoint { get; }

    /// <summary>Gets the token endpoint — <c>token_endpoint</c>.</summary>
    public string? TokenEndpoint { get; }

    /// <summary>Gets the JSON Web Key Set URL — <c>jwks_uri</c>.</summary>
    public string? JwksUri { get; }

    /// <summary>Gets the dynamic client registration endpoint — <c>registration_endpoint</c>.</summary>
    public string? RegistrationEndpoint { get; }

    /// <summary>Gets the supported scopes — <c>scopes_supported</c>; empty when not published.</summary>
    public IReadOnlyList<string> ScopesSupported { get; }

    /// <summary>Gets the supported response types — <c>response_types_supported</c>.</summary>
    public IReadOnlyList<string> ResponseTypesSupported { get; }

    /// <summary>Gets the supported grant types — <c>grant_types_supported</c>, or RFC 8414's default <c>authorization_code</c> and <c>implicit</c> when omitted.</summary>
    public IReadOnlyList<string> GrantTypesSupported { get; }

    /// <summary>Gets the token endpoint's client authentication methods, or RFC 8414's default <c>client_secret_basic</c> when omitted.</summary>
    public IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; }

    /// <summary>Gets the supported PKCE methods — <c>code_challenge_methods_supported</c>; empty means PKCE is not supported.</summary>
    public IReadOnlyList<string> CodeChallengeMethodsSupported { get; }

    /// <summary>Gets the revocation endpoint — <c>revocation_endpoint</c>.</summary>
    public string? RevocationEndpoint { get; }

    /// <summary>Gets the introspection endpoint — <c>introspection_endpoint</c>.</summary>
    public string? IntrospectionEndpoint { get; }

    /// <summary>Gets the RFC 8628 device authorization endpoint — <c>device_authorization_endpoint</c>.</summary>
    public string? DeviceAuthorizationEndpoint { get; }

    /// <summary>Gets the RFC 9126 pushed authorization request endpoint — <c>pushed_authorization_request_endpoint</c>.</summary>
    public string? PushedAuthorizationRequestEndpoint { get; }

    /// <summary>Gets the algorithms accepted for DPoP proofs — <c>dpop_signing_alg_values_supported</c>; empty when not published.</summary>
    public IReadOnlyList<string> DpopSigningAlgValuesSupported { get; }

    /// <summary>
    /// Gets the resources the server lists — <c>protected_resources</c> (RFC 9728 §4) — or null when it does not
    /// enumerate them. A server may leave resources out, so absence from the list is not a refusal.
    /// </summary>
    public IReadOnlyList<string>? ProtectedResources { get; }

    internal static AuthorizationServerMetadataDocument Parse(Uri source, JsonElement json) => new(source, json);
}

/// <summary>
/// What a client needs to obtain a token for a protected resource: the resource's metadata, and the metadata of the
/// authorization server chosen for it.
/// </summary>
/// <param name="ProtectedResource">The validated protected resource metadata.</param>
/// <param name="AuthorizationServer">The validated metadata of the authorization server chosen from its <c>authorization_servers</c>.</param>
public sealed record OAuthDiscoveryResult(ProtectedResourceMetadataDocument ProtectedResource, AuthorizationServerMetadataDocument AuthorizationServer);
