using Microsoft.Extensions.Options;
using static Wiaoj.WellKnown.WellKnownValidation;

namespace Wiaoj.WellKnown;

/// <summary>
/// Rejects authorization server options that would publish a document RFC 8414 forbids, or that clients would misread.
/// </summary>
/// <remarks>
/// Registered for every server added with <c>AddOAuthAuthorizationServer</c> and run at startup. Every rule is checked
/// and every failure reported in one result.
/// </remarks>
internal sealed class OAuthAuthorizationServerOptionsValidator(AuthorizationServerRegistry registry) : IValidateOptions<OAuthAuthorizationServerOptions> {
    private const string Rfc8414 = "RFC 8414";

    /// <summary>What clients assume when <c>grant_types_supported</c> is omitted (§2).</summary>
    private static readonly string[] DefaultGrantTypes = ["authorization_code", "implicit"];

    /// <summary>The grant types that send the user agent to the authorization endpoint.</summary>
    private static readonly string[] AuthorizationEndpointGrantTypes = ["authorization_code", "implicit"];

    /// <summary>The client authentication methods whose JWTs need a declared signing algorithm (§2).</summary>
    private static readonly string[] JwtAuthMethods = ["private_key_jwt", "client_secret_jwt"];

    public ValidateOptionsResult Validate(string? name, OAuthAuthorizationServerOptions options) {
        string serverName = name ?? Options.DefaultName;

        // Options of the same type requested under a name nobody registered are not an authorization server.
        if(!registry.Contains(serverName)) {
            return ValidateOptionsResult.Skip;
        }

        List<string> failures = [];
        string label = serverName.Length == 0 ? "The authorization server" : $"The authorization server '{serverName}'";

        RequireIdentifier(
            options.Issuer,
            label,
            "Issuer",
            "RFC 8414 requires it, and a client refuses the document unless it is identical to the issuer the client asked " +
            "about, so it must be configured as the public URL — it is never taken from the request.",
            failures);

        if(options.ResponseTypesSupported.Count == 0) {
            failures.Add($"{label} has no ResponseTypesSupported; RFC 8414 requires response_types_supported.");
        }

        ValidateRequiredEndpoints(options, label, failures);

        OptionalUrl(options.AuthorizationEndpoint, $"{label} authorization_endpoint", requireHttps: true, failures);
        OptionalUrl(options.TokenEndpoint, $"{label} token_endpoint", requireHttps: true, failures);
        OptionalUrl(options.JwksUri, $"{label} jwks_uri", requireHttps: true, failures);
        OptionalUrl(options.RegistrationEndpoint, $"{label} registration_endpoint", requireHttps: true, failures);
        OptionalUrl(options.RevocationEndpoint, $"{label} revocation_endpoint", requireHttps: true, failures);
        OptionalUrl(options.IntrospectionEndpoint, $"{label} introspection_endpoint", requireHttps: true, failures);
        OptionalUrl(options.DeviceAuthorizationEndpoint, $"{label} device_authorization_endpoint", requireHttps: true, failures);
        OptionalUrl(options.PushedAuthorizationRequestEndpoint, $"{label} pushed_authorization_request_endpoint", requireHttps: true, failures);
        OptionalUrl(options.ServiceDocumentation, $"{label} service_documentation", requireHttps: false, failures);
        OptionalUrl(options.OpPolicyUri, $"{label} op_policy_uri", requireHttps: false, failures);
        OptionalUrl(options.OpTosUri, $"{label} op_tos_uri", requireHttps: false, failures);

        if(options.RequirePushedAuthorizationRequests && string.IsNullOrWhiteSpace(options.PushedAuthorizationRequestEndpoint)) {
            failures.Add(
                $"{label} requires pushed authorization requests but has no PushedAuthorizationRequestEndpoint, so no client " +
                "could make an authorization request.");
        }

        RequireScopeTokens(options.Scopes, label, failures);

        ValidateAuthentication(options.TokenEndpointAuthMethodsSupported, options.TokenEndpointAuthSigningAlgValuesSupported, label, "token_endpoint", failures);
        ValidateAuthentication(options.RevocationEndpointAuthMethodsSupported, options.RevocationEndpointAuthSigningAlgValuesSupported, label, "revocation_endpoint", failures);
        ValidateAuthentication(options.IntrospectionEndpointAuthMethodsSupported, options.IntrospectionEndpointAuthSigningAlgValuesSupported, label, "introspection_endpoint", failures);
        RejectNone(options.DpopSigningAlgValuesSupported, $"{label} dpop_signing_alg_values_supported", "RFC 9449", failures);

        foreach(string resource in options.ProtectedResources) {
            RequireIdentifier(resource, label, "protected resource", "An entry of ProtectedResources is blank.", failures);
        }

        RequireAdditionalParameters(options.AdditionalParameters.Keys, OAuthAuthorizationServerMetadata.StandardParameterNames, label, Rfc8414, failures);
        RequireCacheDuration(options.CacheDuration, label, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// §2: <c>authorization_endpoint</c> is required unless no supported grant type uses it; <c>token_endpoint</c> unless
    /// only the implicit grant is supported. An empty list means the default a client assumes, not "none".
    /// </summary>
    private static void ValidateRequiredEndpoints(OAuthAuthorizationServerOptions options, string label, List<string> failures) {
        IReadOnlyList<string> grantTypes = options.GrantTypesSupported.Count == 0 ? DefaultGrantTypes : options.GrantTypesSupported;
        string listed = options.GrantTypesSupported.Count == 0
            ? "no GrantTypesSupported, which clients read as authorization_code and implicit"
            : $"grant types {string.Join(", ", grantTypes)}";

        if(string.IsNullOrWhiteSpace(options.AuthorizationEndpoint) && grantTypes.Any(AuthorizationEndpointGrantTypes.Contains)) {
            failures.Add($"{label} has {listed}, which use the authorization endpoint, but no AuthorizationEndpoint.");
        }

        bool onlyImplicit = grantTypes.All(grantType => grantType == "implicit");
        if(string.IsNullOrWhiteSpace(options.TokenEndpoint) && !onlyImplicit) {
            failures.Add($"{label} has {listed}, which use the token endpoint, but no TokenEndpoint.");
        }
    }

    private static void ValidateAuthentication(List<string> methods, List<string> algorithms, string label, string endpoint, List<string> failures) {
        if(algorithms.Count == 0 && methods.FirstOrDefault(JwtAuthMethods.Contains) is { } method) {
            failures.Add(
                $"{label} supports '{method}' at the {endpoint} but lists no {endpoint}_auth_signing_alg_values_supported, " +
                "which RFC 8414 requires; no default algorithm is implied.");
        }

        RejectNone(algorithms, $"{label} {endpoint}_auth_signing_alg_values_supported", Rfc8414, failures);
    }
}
