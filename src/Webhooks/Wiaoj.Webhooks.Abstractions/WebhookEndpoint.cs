namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a registered webhook endpoint: destination URL, encrypted signing secret,
/// and optional endpoint-specific cryptographic signer and static HTTP headers.
/// </summary>
public sealed record WebhookEndpoint {

    /// <summary>Gets the unique identifier of this endpoint.</summary>
    public WebhookEndpointId Id { get; }

    /// <summary>Gets the URL deliveries for this endpoint should be POSTed to.</summary>
    public Uri TargetUrl { get; }

    /// <summary>
    /// Gets the signing secret for this endpoint, encrypted at rest.
    /// </summary>
    public EncryptedSecret<WebhookSigningContext> Secret { get; }

    /// <summary>
    /// Gets the optional endpoint-specific cryptographic signer overriding the global default pipeline signer.
    /// </summary>
    public IWebhookSigner? CustomSigner { get; init; }

    /// <summary>
    /// Gets the optional custom static HTTP headers emitted with every delivery to this endpoint (e.g. Authorization, Api-Key).
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEndpoint"/> record with standard delivery settings.
    /// </summary>
    /// <param name="id">The identifier of this endpoint.</param>
    /// <param name="targetUrl">The target destination URI.</param>
    /// <param name="secret">The pre-encrypted signing secret.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetUrl"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secret"/> is default.</exception>
    public WebhookEndpoint(
        WebhookEndpointId id,
        Uri targetUrl,
        EncryptedSecret<WebhookSigningContext> secret)
        : this(id, targetUrl, secret, null, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEndpoint"/> record with custom signer and static headers.
    /// </summary>
    /// <param name="id">The identifier of this endpoint.</param>
    /// <param name="targetUrl">The target destination URI.</param>
    /// <param name="secret">The pre-encrypted signing secret.</param>
    /// <param name="customSigner">The optional custom cryptographic signer.</param>
    /// <param name="customHeaders">The optional custom static HTTP headers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetUrl"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secret"/> is default.</exception>
    public WebhookEndpoint(
        WebhookEndpointId id,
        Uri targetUrl,
        EncryptedSecret<WebhookSigningContext> secret,
        IWebhookSigner? customSigner,
        IReadOnlyDictionary<string, string>? customHeaders) {
        Preca.ThrowIfNull(targetUrl);
        Preca.ThrowIfDefault(secret);

        this.Id = id;
        this.TargetUrl = targetUrl;
        this.Secret = secret;
        this.CustomSigner = customSigner;
        this.CustomHeaders = customHeaders;
    }
}

/// <summary>
/// Phantom type marker identifying the secret domain used to encrypt webhook endpoint
/// signing secrets via <c>Wiaoj.Security</c>.
/// </summary>
/// <remarks>
/// Carries no members — its sole purpose is compile-time domain separation: an
/// <c>EncryptedSecret&lt;WebhookSigningContext&gt;</c> cannot be passed to an
/// <c>ISecretProtector&lt;TOtherContext&gt;</c> for a different domain.
/// </remarks>
public readonly struct WebhookSigningContext : ISecretContext;