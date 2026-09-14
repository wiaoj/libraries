using Microsoft.Extensions.Options;
using static Wiaoj.WellKnown.WellKnownValidation;

namespace Wiaoj.WellKnown;

/// <summary>
/// Rejects protected resource options that would publish a document RFC 9728 clients must refuse, or should not trust.
/// </summary>
/// <remarks>
/// Registered for every resource added with <c>AddOAuthProtectedResource</c> and run at startup. Every rule is checked
/// and every failure reported in one result.
/// </remarks>
internal sealed class OAuthProtectedResourceOptionsValidator(ProtectedResourceRegistry registry) : IValidateOptions<OAuthProtectedResourceOptions> {
    private static readonly string[] BearerMethods = ["header", "body", "query"];

    public ValidateOptionsResult Validate(string? name, OAuthProtectedResourceOptions options) {
        string resourceName = name ?? Options.DefaultName;

        // Options of the same type requested under a name nobody registered are not a protected resource.
        if(!registry.Contains(resourceName)) {
            return ValidateOptionsResult.Skip;
        }

        List<string> failures = [];
        string label = resourceName.Length == 0 ? "The protected resource" : $"The protected resource '{resourceName}'";

        RequireIdentifier(
            options.Resource,
            label,
            "Resource",
            "RFC 9728 requires it, and a client refuses the document unless it is identical to the identifier the client asked " +
            "about, so it must be configured as the public URL — it is never taken from the request.",
            failures);

        foreach(string server in options.AuthorizationServers) {
            RequireUrl(server, $"{label} authorization server", requireHttps: true, failures);
        }

        OptionalUrl(options.JwksUri, $"{label} jwks_uri", requireHttps: true, failures);
        OptionalUrl(options.ResourceDocumentation, $"{label} resource_documentation", requireHttps: false, failures);
        OptionalUrl(options.ResourcePolicyUri, $"{label} resource_policy_uri", requireHttps: false, failures);
        OptionalUrl(options.ResourceTosUri, $"{label} resource_tos_uri", requireHttps: false, failures);

        RequireScopeTokens(options.Scopes, label, failures);

        foreach(string method in options.BearerMethodsSupported) {
            if(!BearerMethods.Contains(method, StringComparer.Ordinal)) {
                failures.Add($"{label} bearer method '{method}' is not one of header, body, query.");
            }
        }

        RejectNone(options.ResourceSigningAlgValuesSupported, $"{label} resource_signing_alg_values_supported", "RFC 9728", failures);
        RejectNone(options.DpopSigningAlgValuesSupported, $"{label} dpop_signing_alg_values_supported", "RFC 9728", failures);

        RequireAdditionalParameters(
            options.AdditionalParameters.Keys,
            OAuthProtectedResourceMetadata.StandardParameterNames,
            label,
            "RFC 9728",
            failures);

        RequireCacheDuration(options.CacheDuration, label, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
