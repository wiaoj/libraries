using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Wiaoj.Primitives.Hashing;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.Signing;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130

/// <summary>
/// Defines a named security and processing policy for inbound webhook endpoints.
/// </summary>
public sealed class WebhookReceiverPolicy {
    /// <summary>Default maximum allowable request body size in bytes (64 KB).</summary>
    public const int DefaultMaxRequestBodyBytes = 64 * 1024;

    /// <summary>Default allowable clock drift tolerance (5 minutes).</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Default idempotency validity window (24 hours).</summary>
    public static readonly TimeSpan DefaultIdempotencyWindow = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the policy name identifier.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets or sets the signature HTTP header name. Default is <c>"Webhook-Signature"</c>.</summary>
    public string HeaderName { get; set; } = WebhookHeaderNames.WebhookSignature;

    /// <summary>Gets or sets the cryptographic signer used for signature verification.</summary>
    public IWebhookSigner Signer { get; set; } = new HmacSha256WebhookSigner();

    /// <summary>Gets or sets the maximum allowable clock skew tolerance.</summary>
    public TimeSpan Tolerance { get; set; } = DefaultTolerance;

    /// <summary>Gets or sets the maximum allowable request body size in bytes to prevent DoS attacks.</summary>
    public int MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

    /// <summary>Gets or sets whether signature verification is strictly required. Default is <see langword="true"/>.</summary>
    public bool RequireSignature { get; set; } = true;

    /// <summary>Gets or sets whether inbound idempotency deduplication is enforced. Default is <see langword="true"/>.</summary>
    public bool EnforceIdempotency { get; set; } = true;

    /// <summary>Gets or sets the deduplication validity window.</summary>
    public TimeSpan IdempotencyWindow { get; set; } = DefaultIdempotencyWindow;

    /// <summary>Gets or sets the secret resolver strategy for this policy.</summary>
    public IWebhookSecretResolver? SecretResolver { get; set; }

    /// <summary>Gets or sets the strategy used to derive an idempotency key from an incoming request.</summary>
    public Func<HttpContext, ReadOnlyMemory<byte>, IdempotencyKey?> IdempotencyKeyExtractor { get; set; } = DefaultIdempotencyKeyExtractor;

    /// <summary>
    /// Derives an idempotency key inspecting standard headers (e.g. <c>Webhook-Id</c>) or falling back to SIMD-accelerated payload digest.
    /// </summary>
    public static IdempotencyKey? DefaultIdempotencyKeyExtractor(HttpContext httpContext, ReadOnlyMemory<byte> rawPayload) {
        string? deliveryId = httpContext.Request.Headers[WebhookHeaderNames.WebhookId].FirstOrDefault();
        if(!string.IsNullOrWhiteSpace(deliveryId)) {
            return new IdempotencyKey($"inbound:id:{deliveryId}");
        }

        XxHash128 hash = XxHash128.Compute(rawPayload.Span);
        return new IdempotencyKey($"inbound:hash:{hash}");
    }

    /// <summary>
    /// Configures an unmanaged <see cref="Secret{Byte}"/> in GC-immune memory for signature verification.
    /// </summary>
    public WebhookReceiverPolicy UseSecret(Secret<byte> secret) {
        this.SecretResolver = new SecretWebhookSecretResolver(secret);
        return this;
    }

    /// <summary>
    /// Configures an encrypted-at-rest secret, unprotecting it dynamically during verification.
    /// </summary>
    public WebhookReceiverPolicy UseEncryptedSecret(
        EncryptedSecret<WebhookSigningContext> encryptedSecret,
        ISecretProtector<WebhookSigningContext> protector) {
        this.SecretResolver = new EncryptedWebhookSecretResolver(encryptedSecret, protector);
        return this;
    }

    /// <summary>
    /// Configures a custom secret resolver strategy.
    /// </summary>
    public WebhookReceiverPolicy UseSecretResolver(IWebhookSecretResolver resolver) {
        Preca.ThrowIfNull(resolver);
        this.SecretResolver = resolver;
        return this;
    }

    /// <summary>
    /// Configures HMAC-SHA256 signing scheme with the specified header name.
    /// </summary>
    public WebhookReceiverPolicy UseHmacSha256(string headerName = WebhookHeaderNames.WebhookSignature) {
        this.HeaderName = headerName;
        this.Signer = new HmacSha256WebhookSigner(headerName);
        return this;
    }

    /// <summary>
    /// Configures HMAC-SHA512 signing scheme with the specified header name.
    /// </summary>
    public WebhookReceiverPolicy UseHmacSha512(string headerName = WebhookHeaderNames.WebhookSignature) {
        this.HeaderName = headerName;
        this.Signer = new HmacSha512WebhookSigner(headerName);
        return this;
    }

    /// <summary>
    /// Binds policy settings directly from an <see cref="IConfiguration"/> section.
    /// </summary>
    public WebhookReceiverPolicy FromConfiguration(IConfiguration configuration) {
        Preca.ThrowIfNull(configuration);

        string? secretStr = configuration["Secret"];
        if(!string.IsNullOrWhiteSpace(secretStr)) {
            this.SecretResolver = new SecretWebhookSecretResolver(Secret.From(secretStr));
        }

        if(TimeSpan.TryParse(configuration["Tolerance"], out TimeSpan tol)) {
            this.Tolerance = tol;
        }

        if(int.TryParse(configuration["MaxBodyBytes"], out int maxBytes)) {
            this.MaxRequestBodyBytes = maxBytes;
        }

        return this;
    }
}