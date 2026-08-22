namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a registered webhook endpoint: where a delivery should be sent and the
/// secret used to sign it.
/// </summary>
public sealed record WebhookEndpoint {

    /// <summary>The identifier of this endpoint.</summary>
    public WebhookEndpointId Id { get; }

    /// <summary>The URL deliveries for this endpoint should be POSTed to.</summary>
    public Uri TargetUrl { get; }

    /// <summary>
    /// The signing secret for this endpoint, encrypted at rest
    /// </summary>
    public EncryptedSecret<WebhookSigningContext> Secret { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEndpoint"/> record.
    /// </summary>
    /// <param name="id">The identifier of this endpoint.</param>
    /// <param name="targetUrl">The URL deliveries should be POSTed to. Cannot be <see langword="null"/>.</param>
    /// <param name="secret">The signing secret. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    public WebhookEndpoint(WebhookEndpointId id, Uri targetUrl, EncryptedSecret<WebhookSigningContext> secret) {
        Preca.ThrowIfNull(targetUrl);
        Preca.ThrowIfDefault(secret);

        this.Id = id;
        this.TargetUrl = targetUrl;
        this.Secret = secret;
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