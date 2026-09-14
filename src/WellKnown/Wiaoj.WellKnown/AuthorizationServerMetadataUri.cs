namespace Wiaoj.WellKnown;

/// <summary>
/// Derives where an authorization server's metadata document is served, from its issuer identifier (RFC 8414 §3).
/// </summary>
/// <remarks>
/// Differs from <see cref="ProtectedResourceMetadataUri"/> in one detail the RFCs do not share: RFC 8414 §3.1 removes any
/// terminating slash of the issuer's path, where RFC 9728 removes only the slash that follows the host.
/// </remarks>
public static class AuthorizationServerMetadataUri {
    /// <summary>The well-known URI suffix registered by RFC 8414.</summary>
    public const string WellKnownPath = "/.well-known/oauth-authorization-server";

    /// <summary>
    /// Returns the path the metadata document for <paramref name="issuer"/> is served at.
    /// </summary>
    /// <param name="issuer">An absolute issuer identifier with no query or fragment.</param>
    /// <returns>
    /// <c>/.well-known/oauth-authorization-server</c> followed by the issuer's path without its terminating slash:
    /// <c>https://auth.example.com</c> gives the suffix alone, <c>https://auth.example.com/tenant1/</c> gives
    /// <c>…/tenant1</c>.
    /// </returns>
    /// <exception cref="ArgumentException">The issuer is not an absolute URL, or has a query or fragment.</exception>
    public static string PathFor(string issuer) {
        return WellKnownUri.PathFor(Parse(issuer), WellKnownPath, trimTerminatingSlash: true);
    }

    /// <summary>
    /// Returns the absolute URL of the metadata document for <paramref name="issuer"/>.
    /// </summary>
    /// <param name="issuer">An absolute issuer identifier with no query or fragment.</param>
    /// <returns>The issuer's scheme and authority, followed by <see cref="PathFor"/>.</returns>
    /// <exception cref="ArgumentException">The issuer is not an absolute URL, or has a query or fragment.</exception>
    public static Uri For(string issuer) {
        Uri uri = Parse(issuer);
        return WellKnownUri.Absolute(uri, WellKnownUri.PathFor(uri, WellKnownPath, trimTerminatingSlash: true));
    }

    private static Uri Parse(string issuer) {
        return WellKnownUri.Parse(issuer, "An issuer identifier (RFC 8414 §2)", nameof(issuer));
    }
}
