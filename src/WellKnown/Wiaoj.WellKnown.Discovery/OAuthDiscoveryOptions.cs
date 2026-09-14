using Wiaoj.Preconditions;

namespace Wiaoj.WellKnown.Discovery;

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
        Preca.ThrowIfNegativeOrZero(this.MaxCachedDocuments, nameof(this.MaxCachedDocuments));
        Preca.ThrowIfNegative(this.MaxCacheDuration, nameof(this.MaxCacheDuration));
        Preca.ThrowIfNegative(this.ChallengeRefreshInterval, nameof(this.ChallengeRefreshInterval));
        Preca.ThrowIfNegative(this.MaxRedirects, nameof(this.MaxRedirects));
        Preca.ThrowIfNegativeOrZero(this.MaxDocumentBytes, nameof(this.MaxDocumentBytes));
    }
}
