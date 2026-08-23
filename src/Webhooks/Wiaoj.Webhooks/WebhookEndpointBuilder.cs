using System.Net;
using Wiaoj.Abstractions;
using Wiaoj.Security;
using Wiaoj.Webhooks.Exceptions;
using Wiaoj.Webhooks.Security;

namespace Wiaoj.Webhooks;

/// <summary>
/// Fluent asynchronous builder for securely constructing and validating <see cref="WebhookEndpoint"/> instances.
/// </summary>
/// <remarks>
/// Performs proactive asynchronous security checks during construction (such as DNS-level SSRF resolution
/// and cryptographic secret encryption) to prevent malformed or dangerous endpoints from being registered.
/// </remarks>
public sealed class WebhookEndpointBuilder : IAsyncBuilder<WebhookEndpoint> {
    private WebhookEndpointId _id;
    private Uri? _targetUrl;
    private EncryptedSecret<WebhookSigningContext> _secret;
    private bool _validateSsrf = true;
    private bool _allowPrivateNetworks;

    /// <summary>
    /// Sets the unique endpoint identifier.
    /// </summary>
    /// <param name="id">The endpoint identifier.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithId(WebhookEndpointId id) {
        this._id = id;
        return this;
    }

    /// <summary>
    /// Sets the unique endpoint identifier from a raw string.
    /// </summary>
    /// <param name="id">The endpoint identifier string.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithId(string id) {
        this._id = WebhookEndpointId.Parse(id);
        return this;
    }

    /// <summary>
    /// Sets the destination target URL.
    /// </summary>
    /// <param name="targetUrl">The target destination URI.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithTargetUrl(Uri targetUrl) {
        Preca.ThrowIfNull(targetUrl);
        this._targetUrl = targetUrl;
        return this;
    }

    /// <summary>
    /// Sets the destination target URL from a URI string.
    /// </summary>
    /// <param name="targetUrl">The target destination URI string.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithTargetUrl(string targetUrl) {
        Preca.ThrowIfNullOrWhiteSpace(targetUrl);
        this._targetUrl = new Uri(targetUrl, UriKind.Absolute);
        return this;
    }

    /// <summary>
    /// Sets the pre-encrypted signing secret.
    /// </summary>
    /// <param name="secret">The encrypted secret.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithSecret(EncryptedSecret<WebhookSigningContext> secret) {
        Preca.ThrowIfDefault(secret);
        this._secret = secret;
        return this;
    }

    /// <summary>
    /// Encrypts a raw plain-text secret using the provided secret protector and attaches it to the endpoint.
    /// </summary>
    /// <param name="plainSecret">The plain-text secret key.</param>
    /// <param name="secretProtector">The secret protector instance.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithSecret(string plainSecret, ISecretProtector<WebhookSigningContext> secretProtector) {
        Preca.ThrowIfNullOrWhiteSpace(plainSecret);
        Preca.ThrowIfNull(secretProtector);

        this._secret = secretProtector.Protect(plainSecret);
        return this;
    }

    /// <summary>
    /// Configures whether asynchronous DNS-level SSRF validation is performed during construction. Default is <see langword="true"/>.
    /// </summary>
    /// <param name="validate">When <see langword="true"/>, validates destination IPs against prohibited private and cloud metadata ranges.</param>
    /// <param name="allowPrivateNetworks">When <see langword="true"/>, permits private and loopback destinations (development mode only).</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public WebhookEndpointBuilder WithSsrfValidation(bool validate = true, bool allowPrivateNetworks = false) {
        this._validateSsrf = validate;
        this._allowPrivateNetworks = allowPrivateNetworks;
        return this;
    }

    /// <summary>
    /// Asynchronously validates network safety and materializes the <see cref="WebhookEndpoint"/> instance.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A fully configured and validated <see cref="WebhookEndpoint"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required properties (ID, URL, or Secret) are missing.</exception>
    /// <exception cref="WebhookSsrfBlockedException">Thrown when target URL resolves to prohibited IP addresses.</exception>
    public async Task<WebhookEndpoint> BuildAsync(CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(this._id.Value, static () => new InvalidOperationException("Endpoint ID must be configured."));
        Preca.ThrowIfNull(this._targetUrl, static () => new InvalidOperationException("Target URL must be configured."));
        Preca.ThrowIfDefault(this._secret, static () => new InvalidOperationException("Signing secret must be configured."));

        if(this._validateSsrf) {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(this._targetUrl.Host, cancellationToken).ConfigureAwait(false);

            bool isSafe = addresses.Any(ip => WebhookIpFilter.IsAllowed(ip, this._allowPrivateNetworks));
            if(!isSafe) {
                throw new WebhookSsrfBlockedException(
                    $"All resolved IP addresses for target host '{this._targetUrl.Host}' are in prohibited private or link-local ranges.");
            }
        }

        return new WebhookEndpoint(this._id, this._targetUrl, this._secret);
    }
}