namespace Wiaoj.WellKnown;

/// <summary>
/// The fields of the RFC 9116 <c>security.txt</c> file <c>MapSecurityTxt</c> serves at <c>/.well-known/security.txt</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every field RFC 9116 §2.5 defines has a property. <see cref="Contact"/> and <see cref="Expires"/> are required; the
/// rest are optional and omitted when empty. The options are validated at startup and every problem is reported at once.
/// </para>
/// <para>
/// Bind them from configuration so the values live with the deployment, not in code:
/// </para>
/// <code>
/// "SecurityTxt": {
///   "Contact": [ "mailto:security@example.com", "https://example.com/security/report" ],
///   "Expires": "2027-06-30T00:00:00Z",
///   "PreferredLanguages": [ "en", "tr" ]
/// }
/// </code>
/// </remarks>
public sealed class SecurityTxtOptions {
    /// <summary>
    /// Gets the ways to report a vulnerability, most preferred first — <c>Contact</c>. Required.
    /// </summary>
    /// <remarks>
    /// Each value is a URI (RFC 9116 §2.5.3): <c>mailto:</c> for an email address, <c>tel:</c> for a telephone number,
    /// <c>https://</c> for a web page. A bare email address or an <c>http://</c> URL is refused.
    /// </remarks>
    public List<string> Contact { get; } = [];

    /// <summary>
    /// Gets or sets when the file's information stops being valid — <c>Expires</c>. Required.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fixed date, deliberately: a date computed from the start time would move forward on every restart, and the file
    /// would never look stale however outdated its contacts became — the opposite of what the field is for (§5.3).
    /// </para>
    /// <list type="bullet">
    /// <item><description>A date already in the past fails startup.</description></item>
    /// <item><description>A date more than a year ahead (RFC 9116 §2.5.5 recommends less) or less than 30 days away logs a
    /// warning when the endpoint is mapped.</description></item>
    /// </list>
    /// <para>
    /// Written in UTC as an RFC 3339 date-time, to the second.
    /// </para>
    /// </remarks>
    public DateTimeOffset? Expires { get; set; }

    /// <summary>
    /// Gets the URIs of keys to encrypt reports with — <c>Encryption</c>. Never the key itself (§2.5.4).
    /// </summary>
    /// <remarks>For example <c>https://example.com/pgp-key.txt</c>, <c>openpgp4fpr:…</c> or <c>dns:…?type=OPENPGPKEY</c>.</remarks>
    public List<string> Encryption { get; } = [];

    /// <summary>Gets the URIs of pages thanking researchers for past reports — <c>Acknowledgments</c>.</summary>
    public List<string> Acknowledgments { get; } = [];

    /// <summary>
    /// Gets the language tags (RFC 5646) reports may be written in, written as one comma-separated
    /// <c>Preferred-Languages</c> field. When empty, researchers assume English.
    /// </summary>
    public List<string> PreferredLanguages { get; } = [];

    /// <summary>
    /// Gets the URIs this file is published at — <c>Canonical</c>.
    /// </summary>
    /// <remarks>
    /// A researcher SHOULD NOT trust a file retrieved from a URI the canonical fields do not list (§2.5.2), so when set, list
    /// every public URL the file is served at, for example <c>https://example.com/.well-known/security.txt</c>.
    /// </remarks>
    public List<string> Canonical { get; } = [];

    /// <summary>Gets the URIs of the vulnerability disclosure policy — <c>Policy</c>.</summary>
    public List<string> Policy { get; } = [];

    /// <summary>Gets the URIs of security-related job openings — <c>Hiring</c>.</summary>
    public List<string> Hiring { get; } = [];

    /// <summary>
    /// Gets fields beyond those RFC 9116 defines, such as one registered with IANA later (§2.4, §6.2), written after the
    /// standard fields in order.
    /// </summary>
    /// <remarks>
    /// A name RFC 9116 defines is refused: set it through its own property, where it is validated.
    /// </remarks>
    public List<KeyValuePair<string, string>> AdditionalFields { get; } = [];

    /// <summary>
    /// Gets or sets whether <c>/security.txt</c> redirects to <c>/.well-known/security.txt</c>, for clients that still look
    /// at the legacy location (RFC 9116 §3). Defaults to <see langword="true"/>.
    /// </summary>
    public bool RedirectLegacyPath { get; set; } = true;

    /// <summary>
    /// Gets or sets how long clients may cache the file, sent as <c>Cache-Control: public, max-age</c>. Defaults to one
    /// day; <see cref="TimeSpan.Zero"/> sends <c>no-cache</c>.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(1);
}
