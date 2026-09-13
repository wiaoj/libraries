using Microsoft.Extensions.Options;

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

        ValidateResource(options.Resource, label, failures);

        foreach(string server in options.AuthorizationServers) {
            RequireUrl(server, $"{label} authorization server", requireHttps: true, failures);
        }

        if(!string.IsNullOrWhiteSpace(options.JwksUri)) {
            RequireUrl(options.JwksUri, $"{label} jwks_uri", requireHttps: true, failures);
        }

        foreach((string? value, string field) in new[] {
            (options.ResourceDocumentation, "resource_documentation"),
            (options.ResourcePolicyUri, "resource_policy_uri"),
            (options.ResourceTosUri, "resource_tos_uri")
        }) {
            if(!string.IsNullOrWhiteSpace(value)) {
                RequireUrl(value, $"{label} {field}", requireHttps: false, failures);
            }
        }

        foreach(string scope in options.Scopes) {
            if(!IsScopeToken(scope)) {
                failures.Add($"{label} scope '{scope}' is not a valid scope token (RFC 6749 §3.3): it must be non-empty, with no spaces, quotes or backslashes.");
            }
        }

        foreach(string method in options.BearerMethodsSupported) {
            if(!BearerMethods.Contains(method, StringComparer.Ordinal)) {
                failures.Add($"{label} bearer method '{method}' is not one of header, body, query.");
            }
        }

        RejectNone(options.ResourceSigningAlgValuesSupported, $"{label} resource_signing_alg_values_supported", failures);
        RejectNone(options.DpopSigningAlgValuesSupported, $"{label} dpop_signing_alg_values_supported", failures);

        if(options.CacheDuration < TimeSpan.Zero) {
            failures.Add($"{label} CacheDuration is negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// The identifier every client compares against byte for byte, so it must be exact and must not come from a request.
    /// </summary>
    private static void ValidateResource(string? resource, string label, List<string> failures) {
        if(string.IsNullOrWhiteSpace(resource)) {
            failures.Add(
                $"{label} has no Resource. RFC 9728 requires it, and a client refuses the document unless it is identical to the " +
                "identifier the client asked about, so it must be configured as the public URL — it is never taken from the request.");
            return;
        }

        if(!Uri.TryCreate(resource, UriKind.Absolute, out Uri? uri)) {
            failures.Add($"{label} Resource '{resource}' is not an absolute URL.");
            return;
        }

        if(!IsHttpsOrLoopbackHttp(uri)) {
            failures.Add($"{label} Resource '{resource}' must use https (http is accepted only on a loopback host, for development).");
        }

        if(resource.Contains('#') || resource.Contains('?')) {
            failures.Add($"{label} Resource '{resource}' has a query or fragment; a resource identifier has neither.");
        }

        if(!string.IsNullOrEmpty(uri.UserInfo)) {
            failures.Add($"{label} Resource '{resource}' contains user information.");
        }
    }

    private static void RequireUrl(string value, string label, bool requireHttps, List<string> failures) {
        if(!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) {
            failures.Add($"{label} '{value}' is not an absolute http or https URL.");
            return;
        }

        if(requireHttps && !IsHttpsOrLoopbackHttp(uri)) {
            failures.Add($"{label} '{value}' must use https (http is accepted only on a loopback host, for development).");
        }
    }

    private static bool IsHttpsOrLoopbackHttp(Uri uri) {
        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }

    private static void RejectNone(List<string> algorithms, string label, List<string> failures) {
        if(algorithms.Contains("none", StringComparer.Ordinal)) {
            failures.Add($"{label} contains 'none', which RFC 9728 forbids.");
        }
    }

    /// <summary>RFC 6749 §3.3: <c>scope-token = 1*( %x21 / %x23-5B / %x5D-7E )</c>.</summary>
    private static bool IsScopeToken(string scope) {
        if(scope.Length == 0) {
            return false;
        }

        foreach(char c in scope) {
            if(c is not ('\x21' or (>= '\x23' and <= '\x5B') or (>= '\x5D' and <= '\x7E'))) {
                return false;
            }
        }

        return true;
    }
}
