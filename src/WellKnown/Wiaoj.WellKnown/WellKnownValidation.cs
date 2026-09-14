namespace Wiaoj.WellKnown;

/// <summary>
/// The checks every well-known document's options share. Each adds a message to <c>failures</c> rather than throwing, so
/// a validator reports every problem at once.
/// </summary>
internal static class WellKnownValidation {
    /// <summary>
    /// An identifier a client compares byte for byte — a resource identifier or an issuer. It must be exact, and must not
    /// come from a request.
    /// </summary>
    public static void RequireIdentifier(string? identifier, string label, string property, string missingReason, List<string> failures) {
        if(string.IsNullOrWhiteSpace(identifier)) {
            failures.Add($"{label} has no {property}. {missingReason}");
            return;
        }

        if(!Uri.TryCreate(identifier, UriKind.Absolute, out Uri? uri)) {
            failures.Add($"{label} {property} '{identifier}' is not an absolute URL.");
            return;
        }

        if(!IsHttpsOrLoopbackHttp(uri)) {
            failures.Add($"{label} {property} '{identifier}' must use https (http is accepted only on a loopback host, for development).");
        }

        if(identifier.Contains('#') || identifier.Contains('?')) {
            failures.Add($"{label} {property} '{identifier}' has a query or fragment; an identifier has neither.");
        }

        if(!string.IsNullOrEmpty(uri.UserInfo)) {
            failures.Add($"{label} {property} '{identifier}' contains user information.");
        }
    }

    /// <summary>Requires an absolute http or https URL; with <paramref name="requireHttps"/>, https or loopback http.</summary>
    public static void RequireUrl(string value, string label, bool requireHttps, List<string> failures) {
        if(!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) {
            failures.Add($"{label} '{value}' is not an absolute http or https URL.");
            return;
        }

        if(requireHttps && !IsHttpsOrLoopbackHttp(uri)) {
            failures.Add($"{label} '{value}' must use https (http is accepted only on a loopback host, for development).");
        }
    }

    /// <summary>Checks <paramref name="value"/> with <see cref="RequireUrl"/> when it is set.</summary>
    public static void OptionalUrl(string? value, string label, bool requireHttps, List<string> failures) {
        if(!string.IsNullOrWhiteSpace(value)) {
            RequireUrl(value, label, requireHttps, failures);
        }
    }

    public static bool IsHttpsOrLoopbackHttp(Uri uri) {
        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }

    public static void RejectNone(IEnumerable<string> algorithms, string label, string specification, List<string> failures) {
        if(algorithms.Contains("none", StringComparer.Ordinal)) {
            failures.Add($"{label} contains 'none', which {specification} forbids.");
        }
    }

    public static void RequireScopeTokens(IEnumerable<string> scopes, string label, List<string> failures) {
        foreach(string scope in scopes) {
            if(!IsScopeToken(scope)) {
                failures.Add($"{label} scope '{scope}' is not a valid scope token (RFC 6749 §3.3): it must be non-empty, with no spaces, quotes or backslashes.");
            }
        }
    }

    /// <summary>Rejects an unnamed additional parameter, or one that would overwrite a validated standard parameter.</summary>
    public static void RequireAdditionalParameters(
        IEnumerable<string> names,
        IReadOnlySet<string> standardNames,
        string label,
        string specification,
        List<string> failures) {

        foreach(string parameter in names) {
            if(string.IsNullOrWhiteSpace(parameter)) {
                failures.Add($"{label} has an additional parameter with an empty name.");
            }
            else if(standardNames.Contains(parameter)) {
                failures.Add(
                    $"{label} additional parameter '{parameter}' is defined by {specification}; set it through its own option, where it is " +
                    "validated, instead of publishing it unchecked.");
            }
        }
    }

    public static void RequireCacheDuration(TimeSpan duration, string label, List<string> failures) {
        if(duration < TimeSpan.Zero) {
            failures.Add($"{label} CacheDuration is negative.");
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
