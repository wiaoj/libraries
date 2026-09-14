using Wiaoj.Preconditions;

namespace Wiaoj.WellKnown;

/// <summary>
/// Inserts a well-known URI suffix between an identifier's host and path — the construction RFC 8414 §3 and RFC 9728 §3
/// share, differing only in which slash they remove.
/// </summary>
internal static class WellKnownUri {
    /// <summary>
    /// Parses an identifier that a well-known document is derived from: absolute, with no query or fragment.
    /// </summary>
    /// <exception cref="ArgumentException">The identifier is not an absolute URL, or has a query or fragment.</exception>
    public static Uri Parse(string identifier, string kind, string parameterName) {
        Preca.ThrowIfNullOrWhiteSpace(identifier, parameterName);

        if(!Uri.TryCreate(identifier, UriKind.Absolute, out Uri? uri)) {
            throw new ArgumentException($"'{identifier}' is not an absolute URL.", parameterName);
        }

        if(uri.Query.Length > 0 || uri.Fragment.Length > 0 || identifier.Contains('#') || identifier.Contains('?')) {
            throw new ArgumentException(
                $"'{identifier}' has a query or fragment. {kind} has neither, and a query cannot be routed to a well-known " +
                "document, so neither is supported.", parameterName);
        }

        return uri;
    }

    /// <summary>
    /// Returns <paramref name="suffix"/> followed by the identifier's path.
    /// </summary>
    /// <param name="identifier">The parsed identifier.</param>
    /// <param name="suffix">The well-known suffix, starting with <c>/.well-known/</c>.</param>
    /// <param name="trimTerminatingSlash">
    /// <see langword="true"/> for RFC 8414, which removes any terminating slash of the path; <see langword="false"/> for
    /// RFC 9728, which removes only the slash that follows the host.
    /// </param>
    public static string PathFor(Uri identifier, string suffix, bool trimTerminatingSlash) {
        string path = identifier.AbsolutePath;

        if(trimTerminatingSlash) {
            path = path.TrimEnd('/');
        }
        else if(path == "/") {
            path = string.Empty;
        }

        return suffix + path;
    }

    /// <summary>Returns the identifier's scheme and authority followed by <paramref name="path"/>.</summary>
    public static Uri Absolute(Uri identifier, string path) {
        return new Uri(identifier.GetLeftPart(UriPartial.Authority) + path, UriKind.Absolute);
    }
}
