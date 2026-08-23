using Microsoft.AspNetCore.Http;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Signing;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Fluent extension methods for configuring policies, toggles, and security overrides on inbound webhook endpoints.
/// </summary>
public static class WebhookRouteHandlerBuilderExtensions {
    /// <summary>
    /// Associates a registered named policy (e.g. <c>"Stripe"</c>, <c>"GitHub"</c>) with this webhook endpoint.
    /// </summary>
    public static RouteHandlerBuilder UsePolicy(this RouteHandlerBuilder builder, string policyName) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        return builder.ConfigureWebhookMetadata(m => m.PolicyName = policyName);
    }

    /// <summary>
    /// Configures an unmanaged <see cref="Secret{Byte}"/> in GC-immune memory for signature verification.
    /// </summary>
    public static RouteHandlerBuilder WithSecret(this RouteHandlerBuilder builder, Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return builder.WithSecretResolver(new SecretWebhookSecretResolver(secret));
    }

    /// <summary>
    /// Configures an encrypted-at-rest secret, unprotecting it dynamically during verification.
    /// </summary>
    public static RouteHandlerBuilder WithEncryptedSecret(
        this RouteHandlerBuilder builder,
        EncryptedSecret<WebhookSigningContext> encryptedSecret,
        ISecretProtector<WebhookSigningContext> protector) {
        Preca.ThrowIfDefault(encryptedSecret);
        Preca.ThrowIfNull(protector);
        return builder.WithSecretResolver(new EncryptedWebhookSecretResolver(encryptedSecret, protector));
    }

    /// <summary>
    /// Configures a dynamic secret resolver strategy (e.g. Multi-Tenant database lookup).
    /// </summary>
    public static RouteHandlerBuilder WithSecretResolver(this RouteHandlerBuilder builder, IWebhookSecretResolver resolver) {
        Preca.ThrowIfNull(resolver);
        return builder.ConfigureWebhookMetadata(m => m.SecretResolver = resolver);
    }

    /// <summary>
    /// Configures a dynamic secret resolver via a delegate.
    /// </summary>
    public static RouteHandlerBuilder WithSecretResolver(
        this RouteHandlerBuilder builder,
        Func<HttpContext, CancellationToken, ValueTask<Secret<byte>>> resolverDelegate) {
        Preca.ThrowIfNull(resolverDelegate);
        return builder.WithSecretResolver(new DelegateWebhookSecretResolver(resolverDelegate));
    }

    /// <summary>
    /// Explicitly permits unsigned webhook requests (useful for local development or internal networks).
    /// </summary>
    public static RouteHandlerBuilder AllowUnsigned(this RouteHandlerBuilder builder) {
        return builder.ConfigureWebhookMetadata(m => m.RequireSignature = false);
    }

    /// <summary>
    /// Explicitly enforces strict cryptographic signature verification.
    /// </summary>
    public static RouteHandlerBuilder RequireSignature(this RouteHandlerBuilder builder, bool required = true) {
        return builder.ConfigureWebhookMetadata(m => m.RequireSignature = required);
    }

    /// <summary>
    /// Disables inbound idempotency deduplication for this endpoint.
    /// </summary>
    public static RouteHandlerBuilder DisableIdempotency(this RouteHandlerBuilder builder) {
        return builder.ConfigureWebhookMetadata(m => m.EnforceIdempotency = false);
    }

    /// <summary>
    /// Configures the inbound idempotency deduplication window duration.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder, TimeSpan window) {
        return builder.ConfigureWebhookMetadata(m => {
            m.EnforceIdempotency = true;
            m.IdempotencyWindow = window;
        });
    }

    /// <summary>
    /// Configures the maximum allowed request body size in bytes to prevent DoS memory exhaustion attacks.
    /// </summary>
    public static RouteHandlerBuilder WithMaxBodySize(this RouteHandlerBuilder builder, int maxBytes) {
        Preca.ThrowIfLessThan(maxBytes, 1);
        return builder.ConfigureWebhookMetadata(m => m.MaxRequestBodyBytes = maxBytes);
    }

    /// <summary>
    /// Configures the maximum allowable clock drift skew tolerance between sender and receiver.
    /// </summary>
    public static RouteHandlerBuilder WithTolerance(this RouteHandlerBuilder builder, TimeSpan tolerance) {
        return builder.ConfigureWebhookMetadata(m => m.Tolerance = tolerance);
    }

    /// <summary>
    /// Configures a custom signature HTTP header name (e.g. <c>"Stripe-Signature"</c>, <c>"X-Hub-Signature-256"</c>).
    /// </summary>
    public static RouteHandlerBuilder WithHeaderName(this RouteHandlerBuilder builder, string headerName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        return builder.ConfigureWebhookMetadata(m => m.HeaderName = headerName);
    }

    /// <summary>
    /// Configures a custom cryptographic signer instance.
    /// </summary>
    public static RouteHandlerBuilder WithSigner(this RouteHandlerBuilder builder, IWebhookSigner signer) {
        Preca.ThrowIfNull(signer);
        return builder.ConfigureWebhookMetadata(m => m.Signer = signer);
    }

    private static RouteHandlerBuilder ConfigureWebhookMetadata(this RouteHandlerBuilder builder, Action<WebhookReceiverEndpointMetadata> configure) {
        builder.Finally(endpointBuilder => {
            WebhookReceiverEndpointMetadata? metadata = endpointBuilder.Metadata.OfType<WebhookReceiverEndpointMetadata>().FirstOrDefault();
            if(metadata is not null) {
                configure(metadata);
            }
        });
        return builder;
    }
}