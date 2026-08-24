using Microsoft.AspNetCore.Http;
using Wiaoj.Webhooks.AspNetCore.Authentication;

namespace Wiaoj.Webhooks.AspNetCore.Metadata;

/// <summary>
/// Endpoint metadata attached via route builder extensions to configure or override inbound webhook policy behaviors.
/// </summary>
public sealed class WebhookReceiverEndpointMetadata {
    /// <summary>Gets or sets the name of the registered policy to inherit from.</summary>
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
    public IWebhookSecretResolver? SecretResolver { get; set; }

    /// <summary>Gets or sets an override for the idempotency key extraction delegate.</summary>
    public Func<HttpContext, ReadOnlyMemory<byte>, IdempotencyKey?>? IdempotencyKeyExtractor { get; set; }
    
    /// <summary>Gets or sets an override for the event discriminator extractor.</summary>
    public IWebhookEventDiscriminatorExtractor? EventExtractor { get; set; }

    /// <summary>Gets or sets an override indicating whether unhandled incoming events should be gracefully acknowledged with 200 OK.</summary>
    public bool? IgnoreUnhandledEvents { get; set; }
}