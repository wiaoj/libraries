namespace Wiaoj.WellKnown;

/// <summary>
/// Derives where a protected resource's metadata document is served, from its resource identifier (RFC 9728 §3).
/// </summary>
/// <remarks>
/// The one computation both the endpoint mapping and the <c>WWW-Authenticate</c> challenge use, so the URL a client is
/// sent to and the route that answers it cannot differ.
/// </remarks>
public static class ProtectedResourceMetadataUri {
    /// <summary>The well-known URI suffix registered by RFC 9728.</summary>
    public const string WellKnownPath = "/.well-known/oauth-protected-resource";

    /// <summary>
    /// Returns the path the metadata document for <paramref name="resource"/> is served at.
    /// </summary>
    /// <param name="resource">An absolute resource identifier with no query or fragment.</param>
    /// <returns>
    /// <c>/.well-known/oauth-protected-resource</c> followed by the identifier's path: <c>https://api.example.com</c> and
    /// <c>https://api.example.com/</c> give the suffix alone, <c>https://api.example.com/v1</c> gives <c>…/v1</c>.
    /// </returns>
    /// <exception cref="ArgumentException">The identifier is not an absolute URL, or has a query or fragment.</exception>
    /// <remarks>
    /// §3: a terminating slash following the host is removed, then the suffix is inserted between the host and the path.
    /// Only that slash is removed — <c>https://api.example.com/v1/</c> keeps its own trailing slash, because it is part
    /// of a different identifier.
    /// </remarks>
    public static string PathFor(string resource) {
        return WellKnownUri.PathFor(Parse(resource), WellKnownPath, trimTerminatingSlash: false);
    }

    /// <summary>
    /// Returns the absolute URL of the metadata document for <paramref name="resource"/>.
    /// </summary>
    /// <param name="resource">An absolute resource identifier with no query or fragment.</param>
    /// <returns>The identifier's scheme and authority, followed by <see cref="PathFor"/>.</returns>
    /// <exception cref="ArgumentException">The identifier is not an absolute URL, or has a query or fragment.</exception>
    public static Uri For(string resource) {
        Uri uri = Parse(resource);
        return WellKnownUri.Absolute(uri, WellKnownUri.PathFor(uri, WellKnownPath, trimTerminatingSlash: false));
    }

    private static Uri Parse(string resource) {
        return WellKnownUri.Parse(resource, "A resource identifier (RFC 9728 §1.2)", nameof(resource));
    }
}
