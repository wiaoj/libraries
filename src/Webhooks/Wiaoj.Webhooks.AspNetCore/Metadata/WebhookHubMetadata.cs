namespace Wiaoj.Webhooks.AspNetCore.Metadata;

/// <summary>
/// Endpoint metadata containing all registered event bindings and configuration overrides for a multi-event webhook hub.
/// </summary>
public sealed class WebhookHubMetadata {
    private readonly Dictionary<string, WebhookHubRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the policy name inherited by the hub endpoint.</summary>
    public string? PolicyName { get; set; }

    /// <summary>Gets or sets an override for the signature HTTP header name.</summary>
    public string? HeaderName { get; set; }

    /// <summary>Gets or sets an override for the cryptographic signer.</summary>
    public IWebhookSigner? Signer { get; set; }

    /// <summary>Gets or sets an override for the clock drift tolerance.</summary>
    public TimeSpan? Tolerance { get; set; }

    /// <summary>Gets or sets an override for maximum request body bytes.</summary>
    public int? MaxRequestBodyBytes { get; set; }

    /// <summary>Gets or sets an override indicating whether signature verification is required.</summary>
    public bool? RequireSignature { get; set; }

    /// <summary>Gets or sets an override indicating whether idempotency deduplication is enforced.</summary>
    public bool? EnforceIdempotency { get; set; }

    /// <summary>Gets or sets an override for the deduplication validity window.</summary>
    public TimeSpan? IdempotencyWindow { get; set; }

    /// <summary>Gets or sets an override for the secret resolver strategy.</summary>
    public Authentication.IWebhookSecretResolver? SecretResolver { get; set; }

    /// <summary>Gets or sets an override for the event discriminator extractor.</summary>
    public IWebhookEventDiscriminatorExtractor? EventExtractor { get; set; }

    /// <summary>Gets or sets an override indicating whether unhandled incoming events should be acknowledged with 200 OK.</summary>
    public bool? IgnoreUnhandledEvents { get; set; }


    /// <summary>Gets or sets an override for the JSON payload path to unwrap before deserialization.</summary>
    public string? PayloadPath {
        get;
        set {
            field = value;
            this.PayloadPathSegmentsUtf8 = !string.IsNullOrWhiteSpace(value)
                ? Utf8JsonPayloadNavigator.TokenizePath(value)
                : null;
        }
    }

    /// <summary>Gets the pre-computed UTF-8 path segments for the hub endpoint.</summary>
    public byte[][]? PayloadPathSegmentsUtf8 { get; private set; }

    /// <summary>Gets the collection of registered event bindings.</summary>
    public IReadOnlyDictionary<string, WebhookHubRegistration> Registrations => this._registrations;

    /// <summary>
    /// Registers or updates an event discriminator binding within the hub.
    /// </summary>
    /// <param name="registration">The event registration descriptor.</param>
    public void AddRegistration(WebhookHubRegistration registration) {
        Preca.ThrowIfNull(registration);
        this._registrations[registration.EventName] = registration;
    }

    /// <summary>
    /// Tries to resolve a registered event binding by its wire-format discriminator name.
    /// </summary>
    /// <param name="eventName">The wire-format event discriminator name.</param>
    /// <param name="registration">When this method returns, contains the registration if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a registration was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetRegistration(string eventName, out WebhookHubRegistration? registration) {
        if(string.IsNullOrWhiteSpace(eventName)) {
            registration = null;
            return false;
        }
        return this._registrations.TryGetValue(eventName, out registration);
    }
}