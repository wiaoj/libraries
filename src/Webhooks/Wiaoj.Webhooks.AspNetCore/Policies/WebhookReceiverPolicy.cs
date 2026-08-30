using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Wiaoj.Primitives.Hashing;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.Signing;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130

/// <summary>
/// Defines a named, strongly-typed security and execution policy for inbound webhook ingress endpoints.
/// Configures DoS request body limits, replay tolerance, unmanaged secret verification, and sliding-window idempotency.
/// </summary>
public sealed class WebhookReceiverPolicy {
    /// <summary>
    /// The default maximum allowable request body size in bytes (64 KB).
    /// </summary>
    public const int DefaultMaxRequestBodyBytes = 64 * 1024;

    /// <summary>
    /// The default clock skew drift tolerance for signature timestamp verification (5 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The default sliding validity window during which duplicate inbound events are suppressed (24 hours).
    /// </summary>
    public static readonly TimeSpan DefaultIdempotencyWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the unique policy identifier name (e.g., <c>"Stripe"</c>, <c>"GitHub"</c>, <c>"Shopify"</c>).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP header name transporting the cryptographic signature. Default is <c>"Webhook-Signature"</c>.
    /// </summary>
    public string HeaderName { get; set; } = WebhookHeaderNames.WebhookSignature;

    /// <summary>
    /// Gets or sets the cryptographic signer used to verify incoming signatures. Default is <see cref="HmacSha256WebhookSigner"/>.
    /// </summary>
    public IWebhookSigner Signer { get; set; } = new HmacSha256WebhookSigner();

    /// <summary>
    /// Gets or sets the allowable clock skew drift between sender and receiver. Default is 5 minutes.
    /// </summary>
    public TimeSpan Tolerance { get; set; } = DefaultTolerance;

    /// <summary>
    /// Gets or sets the maximum allowable request body size in bytes to prevent Denial-of-Service memory exhaustion. Default is 64 KB.
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

    /// <summary>
    /// Gets or sets a value indicating whether cryptographic signature verification is strictly required. Default is <see langword="true"/>.
    /// </summary>
    public bool RequireSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether inbound idempotency deduplication is enforced. Default is <see langword="true"/>.
    /// </summary>
    public bool EnforceIdempotency { get; set; } = true;

    /// <summary>
    /// Gets or sets the time window during which duplicate events are deduplicated. Default is 24 hours.
    /// </summary>
    public TimeSpan IdempotencyWindow { get; set; } = DefaultIdempotencyWindow;

    /// <summary>
    /// Gets or sets the secret resolution strategy used to resolve sensitive signing keys in unmanaged memory.
    /// </summary>
    public IWebhookSecretResolver? SecretResolver { get; set; }

    /// <summary>
    /// Gets or sets the delegate strategy used to extract a unique idempotency key from an incoming HTTP request.
    /// </summary>
    public Func<HttpContext, ReadOnlyMemory<byte>, IdempotencyKey?> IdempotencyKeyExtractor { get; set; } = DefaultIdempotencyKeyExtractor;

    /// <summary>
    /// Gets or sets the event discriminator extraction strategy.
    /// Default is <see cref="CompositeEventDiscriminatorExtractor.Default"/>.
    /// </summary>
    public IWebhookEventDiscriminatorExtractor EventExtractor { get; set; } = CompositeEventDiscriminatorExtractor.Default;

    /// <summary>
    /// Gets or sets a value indicating whether unhandled incoming events should be gracefully accepted with 200 OK.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool IgnoreUnhandledEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether inbound loop detection and hop count threshold enforcement is active.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool EnableLoopDetection { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowable hop count before inbound requests are rejected. Default is 5.
    /// </summary>
    public int MaxHops { get; set; } = 5;

    /// <summary>
    /// Gets or sets the HTTP header name carrying the integer hop counter. Default is <see cref="WebhookHeaderNames.WebhookHopCount"/>.
    /// </summary>
    public string HopCountHeaderName { get; set; } = WebhookHeaderNames.WebhookHopCount;

    /// <summary>
    /// Gets or sets the HTTP header name carrying the causal execution chain. Default is <see cref="WebhookHeaderNames.WebhookCausalChain"/>.
    /// </summary>
    public string CausalChainHeaderName { get; set; } = WebhookHeaderNames.WebhookCausalChain;

    /// <summary>
    /// Gets or sets the engine instance ID used for inbound causal cycle detection.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Enables inbound loop detection and hop count limit enforcement using the default limit of 5 hops.
    /// </summary>
    /// <returns>This policy instance for fluent chaining.</returns>
    public WebhookReceiverPolicy WithLoopDetection() {
        return this.WithLoopDetection(5);
    }

    /// <summary>
    /// Enables inbound loop detection and hop count limit enforcement with a custom hop threshold.
    /// </summary>
    /// <param name="maxHops">The maximum allowable hop count.</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxHops"/> is non-positive.</exception>
    public WebhookReceiverPolicy WithLoopDetection(int maxHops) {
        Preca.ThrowIfLessThanOrEqualTo(maxHops, 0);
        this.EnableLoopDetection = true;
        this.MaxHops = maxHops;
        return this;
    }

    /// <summary>
    /// Enables inbound loop detection and hop count limit enforcement with a custom hop threshold and custom header name.
    /// </summary>
    /// <param name="maxHops">The maximum allowable hop count.</param>
    /// <param name="headerName">The custom hop count HTTP header name.</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxHops"/> is non-positive.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="headerName"/> is null, empty, or whitespace.</exception>
    public WebhookReceiverPolicy WithLoopDetection(int maxHops, string headerName) {
        Preca.ThrowIfLessThanOrEqualTo(maxHops, 0);
        Preca.ThrowIfNullOrWhiteSpace(headerName);

        this.EnableLoopDetection = true;
        this.MaxHops = maxHops;
        this.HopCountHeaderName = headerName;
        return this;
    }

    /// <summary>
    /// Configures event discriminator extraction from a specific HTTP header.
    /// </summary>
    /// <param name="headerName">The header name (e.g. <c>"X-GitHub-Event"</c>, <c>"X-Shopify-Topic"</c>).</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    public WebhookReceiverPolicy WithEventFromHeader(string headerName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this.EventExtractor = new HeaderEventDiscriminatorExtractor(headerName);
        return this;
    }

    /// <summary>
    /// Configures event discriminator extraction from a specific root JSON property.
    /// </summary>
    /// <param name="propertyName">The JSON property name (e.g. <c>"type"</c>, <c>"event"</c>).</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    public WebhookReceiverPolicy WithEventFromJsonProperty(string propertyName = "type") {
        Preca.ThrowIfNullOrWhiteSpace(propertyName);
        this.EventExtractor = new JsonPropertyEventDiscriminatorExtractor(propertyName);
        return this;
    }

    /// <summary>
    /// Configures a custom event discriminator extractor strategy.
    /// </summary>
    /// <param name="extractor">The custom discriminator extractor implementation.</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    public WebhookReceiverPolicy WithEventExtractor(IWebhookEventDiscriminatorExtractor extractor) {
        Preca.ThrowIfNull(extractor);
        this.EventExtractor = extractor;
        return this;
    }

    /// <summary>
    /// Configures whether unhandled incoming events should be gracefully acknowledged with 200 OK.
    /// </summary>
    /// <param name="ignore"><see langword="true"/> to acknowledge unhandled events with 200 OK; <see langword="false"/> to return 400 Bad Request.</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    public WebhookReceiverPolicy WithIgnoreUnhandledEvents(bool ignore = true) {
        this.IgnoreUnhandledEvents = ignore;
        return this;
    }

    /// <summary>
    /// Derives an idempotency key by inspecting standard delivery headers (e.g. <c>Webhook-Id</c>)
    /// or computing a SIMD-accelerated 128-bit hash (<see cref="XxHash128"/>) over the raw request body bytes.
    /// </summary>
    /// <param name="httpContext">The active ASP.NET Core HTTP context.</param>
    /// <param name="rawPayload">The raw UTF-8 request body bytes.</param>
    /// <returns>A strongly-typed <see cref="IdempotencyKey"/> instance, or <see langword="null"/> if extraction fails.</returns>
    public static IdempotencyKey? DefaultIdempotencyKeyExtractor(HttpContext httpContext, ReadOnlyMemory<byte> rawPayload) {
        string? deliveryId = httpContext.Request.Headers[WebhookHeaderNames.WebhookId].FirstOrDefault();
        if(!string.IsNullOrWhiteSpace(deliveryId)) {
            return new IdempotencyKey($"inbound:id:{deliveryId}");
        }

        XxHash128 hash = XxHash128.Compute(rawPayload.Span);
        return new IdempotencyKey($"inbound:hash:{hash}");
    }

    /// <summary>
    /// Configures the allowable clock skew drift tolerance for signature timestamp verification.
    /// </summary>
    /// <param name="tolerance">The maximum allowable clock drift.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is negative.</exception>
    public WebhookReceiverPolicy WithTolerance(TimeSpan tolerance) {
        if(tolerance < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance cannot be negative.");
        }
        this.Tolerance = tolerance;
        return this;
    }

    /// <summary>
    /// Configures the maximum allowable request body size in bytes to defend against DoS stream exhaustion.
    /// </summary>
    /// <param name="maxBytes">The maximum body size limit in bytes.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is less than 1.</exception>
    public WebhookReceiverPolicy WithMaxBodySize(int maxBytes) {
        Preca.ThrowIfLessThan(maxBytes, 1);
        this.MaxRequestBodyBytes = maxBytes;
        return this;
    }

    /// <summary>
    /// Enables and configures the inbound idempotency deduplication window duration.
    /// </summary>
    /// <param name="window">The deduplication validity duration.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="window"/> is non-positive.</exception>
    public WebhookReceiverPolicy WithIdempotency(TimeSpan window) {
        Preca.ThrowIfNegativeOrZero(window);
        this.EnforceIdempotency = true;
        this.IdempotencyWindow = window;
        return this;
    }

    /// <summary>
    /// Gets or sets the dot-delimited JSON property path to unwrap before deserialization (e.g. <c>"data.object"</c>).
    /// When <see langword="null"/>, the root JSON payload is deserialized directly.
    /// </summary>
    public string? PayloadPath {
        get;
        set {
            field = value;
            this.PayloadPathSegmentsUtf8 = !string.IsNullOrWhiteSpace(value)
                ? Utf8JsonPayloadNavigator.TokenizePath(value)
                : null;
        }
    }

    /// <summary>
    /// Gets the pre-computed UTF-8 path segments for zero-allocation payload unwrapping.
    /// </summary>
    public byte[][]? PayloadPathSegmentsUtf8 { get; private set; }

    /// <summary>
    /// Configures the dot-delimited JSON property path to unwrap before deserialization (e.g. <c>"data.object"</c>).
    /// </summary>
    /// <param name="payloadPath">The dot-separated JSON path.</param>
    /// <returns>This policy instance for fluent chaining.</returns>
    public WebhookReceiverPolicy WithPayloadPath(string payloadPath) {
        Preca.ThrowIfNullOrWhiteSpace(payloadPath);
        this.PayloadPath = payloadPath;
        return this;
    }

    /// <summary>
    /// Disables inbound idempotency deduplication for this policy.
    /// </summary>
    /// <returns>This policy instance for fluent method chaining.</returns>
    public WebhookReceiverPolicy DisableIdempotency() {
        this.EnforceIdempotency = false;
        return this;
    }

    /// <summary>
    /// Explicitly permits unsigned webhook requests under this policy (useful for local development or internal networks).
    /// </summary>
    /// <returns>This policy instance for fluent method chaining.</returns>
    public WebhookReceiverPolicy AllowUnsigned() {
        this.RequireSignature = false;
        return this;
    }

    /// <summary>
    /// Configures whether signature verification is strictly enforced.
    /// </summary>
    /// <param name="required">When <see langword="true"/>, enforces signature validation; otherwise permits unsigned payloads.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    public WebhookReceiverPolicy WithRequireSignature(bool required = true) {
        this.RequireSignature = required;
        return this;
    }

    /// <summary>
    /// Configures a custom cryptographic signer instance for signature verification under this policy,
    /// automatically synchronizing the policy's header name with the signer's configured header name.
    /// </summary>
    /// <param name="signer">The custom webhook signer implementation.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="signer"/> is <see langword="null"/>.</exception>
    public WebhookReceiverPolicy WithSigner(IWebhookSigner signer) {
        Preca.ThrowIfNull(signer);
        this.Signer = signer;
        this.HeaderName = signer.HeaderName;
        return this;
    }

    /// <summary>
    /// Configures a custom cryptographic signer type for signature verification under this policy.
    /// </summary>
    /// <typeparam name="TSigner">The signer type implementing <see cref="IWebhookSigner"/> with a parameterless constructor.</typeparam>
    /// <returns>This policy instance for fluent method chaining.</returns>
    public WebhookReceiverPolicy WithSigner<TSigner>() where TSigner : class, IWebhookSigner, new() {
        return WithSigner(new TSigner());
    }

    /// <summary>
    /// Configures a custom cryptographic signer instance for signature verification under this policy. Alias for <see cref="WithSigner(IWebhookSigner)"/>.
    /// </summary>
    /// <param name="signer">The custom webhook signer implementation.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    public WebhookReceiverPolicy UseSigner(IWebhookSigner signer) {
        return WithSigner(signer);
    }

    /// <summary>
    /// Configures a custom cryptographic signer type for signature verification under this policy. Alias for <see cref="WithSigner{TSigner}()"/>.
    /// </summary>
    /// <typeparam name="TSigner">The signer type implementing <see cref="IWebhookSigner"/> with a parameterless constructor.</typeparam>
    /// <returns>This policy instance for fluent method chaining.</returns>
    public WebhookReceiverPolicy UseSigner<TSigner>() where TSigner : class, IWebhookSigner, new() {
        return WithSigner<TSigner>();
    }

    /// <summary>
    /// Configures an unmanaged <see cref="Secret{Byte}"/> stored in GC-immune native memory for cryptographic signature verification.
    /// </summary>
    /// <param name="secret">The sensitive secret key held in unmanaged memory.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
    public WebhookReceiverPolicy UseSecret(Secret<byte> secret) {
        this.SecretResolver = new SecretWebhookSecretResolver(secret);
        return this;
    }

    /// <summary>
    /// Configures an encrypted-at-rest secret envelope, unprotecting it into unmanaged memory dynamically during verification.
    /// </summary>
    /// <param name="encryptedSecret">The encrypted secret key envelope.</param>
    /// <param name="protector">The secret protector instance used for on-demand decryption.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encryptedSecret"/> is default.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="protector"/> is <see langword="null"/>.</exception>
    public WebhookReceiverPolicy UseEncryptedSecret(
        EncryptedSecret<WebhookSigningContext> encryptedSecret,
        ISecretProtector<WebhookSigningContext> protector) {
        this.SecretResolver = new EncryptedWebhookSecretResolver(encryptedSecret, protector);
        return this;
    }

    /// <summary>
    /// Configures a custom secret resolver strategy (e.g. dynamic multi-tenant database resolution).
    /// </summary>
    /// <param name="resolver">The secret resolver implementation.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolver"/> is <see langword="null"/>.</exception>
    public WebhookReceiverPolicy UseSecretResolver(IWebhookSecretResolver resolver) {
        Preca.ThrowIfNull(resolver);
        this.SecretResolver = resolver;
        return this;
    }

    /// <summary>
    /// Configures HMAC-SHA256 (Scheme prefix <c>"v1"</c>) signature verification with the specified HTTP header name.
    /// </summary>
    /// <param name="headerName">The HTTP header name transporting the signature. Default is <c>"Webhook-Signature"</c>.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="headerName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookReceiverPolicy UseHmacSha256(string headerName = WebhookHeaderNames.WebhookSignature) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this.HeaderName = headerName;
        this.Signer = new HmacSha256WebhookSigner(headerName);
        return this;
    }

    /// <summary>
    /// Configures HMAC-SHA512 (Scheme prefix <c>"v2"</c>) signature verification with the specified HTTP header name.
    /// </summary>
    /// <param name="headerName">The HTTP header name transporting the signature. Default is <c>"Webhook-Signature"</c>.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="headerName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public WebhookReceiverPolicy UseHmacSha512(string headerName = WebhookHeaderNames.WebhookSignature) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        this.HeaderName = headerName;
        this.Signer = new HmacSha512WebhookSigner(headerName);
        return this;
    }

    /// <summary>
    /// Binds policy configuration settings directly from an <see cref="IConfiguration"/> section (e.g. from <c>appsettings.json</c>).
    /// </summary>
    /// <param name="configuration">The configuration section containing policy settings.</param>
    /// <returns>This policy instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is <see langword="null"/>.</exception>
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