namespace Wiaoj.WellKnown.Discovery;

/// <summary>Why discovery refused to hand over a document.</summary>
public enum OAuthDiscoveryFailure {
    /// <summary>An identifier or metadata URL is not an absolute URL, or has a query or fragment where none is allowed.</summary>
    InvalidUrl,

    /// <summary>A document would have been fetched without TLS.</summary>
    InsecureTransport,

    /// <summary>The metadata endpoint answered with a status other than 200.</summary>
    HttpError,

    /// <summary>A redirect led to another origin, which could substitute another party's document.</summary>
    CrossOriginRedirect,

    /// <summary>More same-origin redirects than <see cref="OAuthDiscoveryOptions.MaxRedirects"/>.</summary>
    TooManyRedirects,

    /// <summary>The response is not a JSON object, a known parameter has the wrong type, or a required one is missing.</summary>
    InvalidDocument,

    /// <summary>The document exceeds <see cref="OAuthDiscoveryOptions.MaxDocumentBytes"/>.</summary>
    DocumentTooLarge,

    /// <summary>
    /// <c>resource</c> or <c>issuer</c> is not the identifier the document was looked up for — RFC 9728 §3.3 and RFC 8414
    /// §3.3 forbid using such a document.
    /// </summary>
    IdentifierMismatch,

    /// <summary>The challenges carry more than one different <c>resource_metadata</c> URL.</summary>
    AmbiguousChallenge,

    /// <summary>The protected resource lists no authorization server.</summary>
    NoAuthorizationServer,

    /// <summary>None of the resource's authorization servers is in <see cref="OAuthDiscoveryOptions.TrustedAuthorizationServers"/>.</summary>
    UntrustedAuthorizationServer
}

/// <summary>
/// Discovery found a document it must not use, or could not fetch one.
/// </summary>
public sealed class OAuthDiscoveryException : Exception {
    /// <summary>Creates the exception.</summary>
    /// <param name="failure">Why discovery failed.</param>
    /// <param name="message">A description naming the URL or identifier involved.</param>
    /// <param name="innerException">The underlying error, if any.</param>
    public OAuthDiscoveryException(OAuthDiscoveryFailure failure, string message, Exception? innerException = null)
        : base(message, innerException) {
        this.Failure = failure;
    }

    /// <summary>Gets why discovery failed.</summary>
    public OAuthDiscoveryFailure Failure { get; }
}
