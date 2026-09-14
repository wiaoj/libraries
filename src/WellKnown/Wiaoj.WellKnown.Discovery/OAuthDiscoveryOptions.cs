namespace Wiaoj.WellKnown.Discovery;

/// <summary>How a resource identifier found through a <c>WWW-Authenticate</c> challenge must relate to the request that was challenged.</summary>
public enum ChallengeResourceMatching {
    /// <summary>
    /// The resource must have the request's origin, and its path must be the request's path or a leading run of its
    /// segments: resource <c>https://api.example.com/v1</c> matches a request to <c>https://api.example.com/v1/users</c>.
    /// </summary>
    /// <remarks>
    /// RFC 9728 §3.3 literally requires the resource to be identical to the request URL. That rejects every API whose
    /// identifier names the API rather than one of its URLs — which is how resource identifiers are used, and what
    /// <c>Wiaoj.WellKnown</c> publishes. The origin and segment-boundary checks keep what the rule protects against: a
    /// resource cannot claim another host, or a sibling path such as <c>/v10</c> for <c>/v1</c>.
    /// </remarks>
    PathPrefix,

    /// <summary>The resource must be identical to the request URL, as RFC 9728 §3.3 is written.</summary>
    Exact
}

/// <summary>
/// Options for <see cref="OAuthDiscoveryClient"/>.
/// </summary>
public sealed class OAuthDiscoveryOptions {
    /// <summary>
    /// Gets the issuers the client may use. When empty, the first authorization server a resource lists is used.
    /// </summary>
    /// <remarks>
    /// RFC 9728 §7.6 leaves which authorization servers to trust to the application, and §7.7 warns that fetching
    /// whatever a resource names is a server-side request forgery risk. Between services that know each other — Verba and
    /// Prism trusting Vaultex — listing the issuer here is the control: a resource naming any other server is refused
    /// with <see cref="OAuthDiscoveryFailure.UntrustedAuthorizationServer"/>, before anything is fetched from it.
    /// Compared ordinally, as issuers are.
    /// </remarks>
    public List<string> TrustedAuthorizationServers { get; } = [];

    /// <summary>Gets or sets how a resource found through a challenge is matched to the request. <see cref="ChallengeResourceMatching.PathPrefix"/> by default.</summary>
    public ChallengeResourceMatching ChallengeResourceMatching { get; set; } = ChallengeResourceMatching.PathPrefix;

    /// <summary>Gets or sets whether <c>http</c> is accepted on a loopback host, for development. <see langword="true"/> by default; every other host requires https.</summary>
    public bool AllowHttpOnLoopback { get; set; } = true;

    /// <summary>Gets or sets how many documents are cached at once. 256 by default.</summary>
    public int MaxCachedDocuments { get; set; } = 256;

    /// <summary>Gets or sets the longest a document is cached, whatever its <c>max-age</c>. One day by default.</summary>
    public TimeSpan MaxCacheDuration { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets how old a cached protected resource document must be before a new challenge re-fetches it. 30
    /// seconds by default.
    /// </summary>
    /// <remarks>
    /// RFC 9728 §5.2: a new <c>resource_metadata</c> challenge signals that the metadata may have changed. Every 401 of a
    /// burst carries one, and re-fetching for each would turn the burst into as many metadata requests.
    /// </remarks>
    public TimeSpan ChallengeRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how many same-origin redirects are followed. 5 by default; a redirect to another origin is never followed.</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>Gets or sets the largest document accepted, in bytes. 1 MiB by default.</summary>
    public int MaxDocumentBytes { get; set; } = 1024 * 1024;

    internal void Validate() {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxCachedDocuments, nameof(this.MaxCachedDocuments));
        ArgumentOutOfRangeException.ThrowIfLessThan(this.MaxCacheDuration, TimeSpan.Zero, nameof(this.MaxCacheDuration));
        ArgumentOutOfRangeException.ThrowIfLessThan(this.ChallengeRefreshInterval, TimeSpan.Zero, nameof(this.ChallengeRefreshInterval));
        ArgumentOutOfRangeException.ThrowIfNegative(this.MaxRedirects, nameof(this.MaxRedirects));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxDocumentBytes, nameof(this.MaxDocumentBytes));
    }
}
