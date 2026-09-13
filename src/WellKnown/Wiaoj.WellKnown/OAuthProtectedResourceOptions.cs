namespace Wiaoj.WellKnown;

/// <summary>
/// Describes one OAuth 2.0 protected resource, as published in its RFC 9728 metadata document.
/// </summary>
/// <remarks>
/// <para>
/// Each property maps to one metadata parameter of RFC 9728 §2. Everything except <see cref="Resource"/> is optional,
/// and an unset parameter — null, an empty list, or <see langword="false"/> — is left out of the document, as §3.2
/// requires.
/// </para>
/// <para>
/// The options are validated when the application starts. Every problem is reported together, so a misconfigured
/// document fails once, with the whole list, rather than one restart per mistake.
/// </para>
/// </remarks>
public sealed class OAuthProtectedResourceOptions {
    /// <summary>
    /// Gets or sets the protected resource's identifier — <c>resource</c>. Required.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An absolute <c>https</c> URL with no query and no fragment, such as <c>https://api.example.com</c> or
    /// <c>https://api.example.com/v1</c>. A client rejects the document unless this is identical to the identifier it
    /// derived the metadata URL from (§3.3), so it must be the public address — never the address the application
    /// happens to see behind a proxy. For local development, <c>http</c> is accepted on a loopback host.
    /// </para>
    /// <para>
    /// The document's URL is derived from it: <c>https://api.example.com/v1</c> is served at
    /// <c>/.well-known/oauth-protected-resource/v1</c>.
    /// </para>
    /// </remarks>
    public string? Resource { get; set; }

    /// <summary>Gets or sets the issuer identifiers of the authorization servers that issue tokens for this resource — <c>authorization_servers</c>.</summary>
    public List<string> AuthorizationServers { get; set; } = [];

    /// <summary>Gets or sets the URL of the resource's JSON Web Key Set — <c>jwks_uri</c>. Must use <c>https</c>.</summary>
    public string? JwksUri { get; set; }

    /// <summary>Gets the scopes used to request access to this resource — <c>scopes_supported</c>. RFC 9728 recommends publishing them.</summary>
    /// <remarks>Modules can add their own with <c>AddProtectedResourceScopes</c>. Written de-duplicated, in ordinal order.</remarks>
    public HashSet<string> Scopes { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the ways an access token may be presented — <c>bearer_methods_supported</c>: <c>header</c>, <c>body</c>, <c>query</c>.</summary>
    public List<string> BearerMethodsSupported { get; } = [];

    /// <summary>Gets the JWS algorithms the resource signs responses with — <c>resource_signing_alg_values_supported</c>. <c>none</c> is not allowed.</summary>
    public List<string> ResourceSigningAlgValuesSupported { get; } = [];

    /// <summary>Gets or sets a human-readable name for the resource — <c>resource_name</c>. RFC 9728 recommends publishing it.</summary>
    public string? ResourceName { get; set; }

    /// <summary>Gets or sets the URL of developer documentation for the resource — <c>resource_documentation</c>.</summary>
    public string? ResourceDocumentation { get; set; }

    /// <summary>Gets or sets the URL of the resource's data usage policy — <c>resource_policy_uri</c>.</summary>
    public string? ResourcePolicyUri { get; set; }

    /// <summary>Gets or sets the URL of the resource's terms of service — <c>resource_tos_uri</c>.</summary>
    public string? ResourceTosUri { get; set; }

    /// <summary>Gets or sets whether the resource supports mutual-TLS certificate-bound access tokens — <c>tls_client_certificate_bound_access_tokens</c>.</summary>
    public bool TlsClientCertificateBoundAccessTokens { get; set; }

    /// <summary>Gets the authorization details types the resource supports — <c>authorization_details_types_supported</c>.</summary>
    public List<string> AuthorizationDetailsTypesSupported { get; } = [];

    /// <summary>Gets the JWS algorithms accepted for DPoP proof JWTs — <c>dpop_signing_alg_values_supported</c>. <c>none</c> is not allowed.</summary>
    public List<string> DpopSigningAlgValuesSupported { get; } = [];

    /// <summary>Gets or sets whether the resource always requires DPoP-bound access tokens — <c>dpop_bound_access_tokens_required</c>.</summary>
    public bool DpopBoundAccessTokensRequired { get; set; }

    /// <summary>
    /// Gets or sets how long clients may cache the document, sent as <c>Cache-Control: public, max-age</c>. Defaults to
    /// one day; <see cref="TimeSpan.Zero"/> sends <c>no-cache</c>.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}
