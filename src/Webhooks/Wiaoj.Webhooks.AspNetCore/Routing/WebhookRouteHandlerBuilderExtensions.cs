using System.Reflection;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.AspNetCore.Diagnostics;
using Wiaoj.Webhooks.AspNetCore.Metadata;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Fluent extension methods for configuring event bindings, policies, and security overrides on webhook endpoints.
/// </summary>
public static class WebhookRouteHandlerBuilderExtensions {

    // ────────────────────────────────────────────────────────────────────────
    // 1. HUB EVENT BINDINGS (.On, .MapHandler, .OnPing)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers an event discriminator binding with an inline Minimal API delegate on the hub.
    /// </summary>
    /// <typeparam name="TEvent">The target payload model type.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="eventName">The wire-format event discriminator name.</param>
    /// <param name="handler">The execution delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder On<TEvent>(
        this RouteHandlerBuilder builder,
        string eventName,
        Delegate handler) where TEvent : class {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(eventName);
        Preca.ThrowIfNull(handler);

        return builder.ConfigureHubMetadata(metadata => {
            metadata.AddRegistration(new WebhookHubRegistration(eventName, typeof(TEvent), handler));
        });
    }

    /// <summary>
    /// Registers an event discriminator binding with an inline delegate, resolving the event name from <see cref="WebhookEventAttribute"/> or convention.
    /// </summary>
    /// <typeparam name="TEvent">The target payload model type.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="handler">The execution delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder On<TEvent>(
        this RouteHandlerBuilder builder,
        Delegate handler) where TEvent : class {
        string eventName = ResolveEventName(typeof(TEvent));
        return builder.On<TEvent>(eventName, handler);
    }

    /// <summary>
    /// Registers an event discriminator binding dispatching to a class-based <see cref="IWebhookReceiverHandler{TEvent}"/>.
    /// </summary>
    /// <typeparam name="TEvent">The target payload model type.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="eventName">The wire-format event discriminator name.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder MapHandler<TEvent>(
        this RouteHandlerBuilder builder,
        string eventName) where TEvent : class {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        return builder.ConfigureHubMetadata(metadata => {
            metadata.AddRegistration(new WebhookHubRegistration(eventName, typeof(TEvent), (Type?)null));
        });
    }

    /// <summary>
    /// Registers an event discriminator binding dispatching to a specific class-based handler.
    /// </summary>
    /// <typeparam name="TEvent">The target payload model type.</typeparam>
    /// <typeparam name="THandler">The handler type implementing <see cref="IWebhookReceiverHandler{TEvent}"/>.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="eventName">The wire-format event discriminator name.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder MapHandler<TEvent, THandler>(
        this RouteHandlerBuilder builder,
        string eventName)
        where TEvent : class
        where THandler : class, IWebhookReceiverHandler<TEvent> {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        return builder.ConfigureHubMetadata(metadata => {
            metadata.AddRegistration(new WebhookHubRegistration(eventName, typeof(TEvent), typeof(THandler)));
        });
    }

    /// <summary>
    /// Registers an automated 200 OK acknowledgment for healthcheck ping events.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="pingEventName">The ping event discriminator name. Defaults to <c>"ping"</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder OnPing(this RouteHandlerBuilder builder, string pingEventName) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(pingEventName);

        return builder.On<object>(pingEventName, static () => WebhookReceiverResponses.Pong);
    }

    /// <summary>
    /// Registers an automated 200 OK acknowledgment for default <c>"ping"</c> and <c>"webhook.ping"</c> events.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder OnPing(this RouteHandlerBuilder builder) {
        return builder.OnPing("ping")
                      .OnPing("webhook.ping");
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. POLICIES & SECURITY OVERRIDES (Unified for Single and Hub Endpoints)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Associates a registered named policy with this webhook endpoint.
    /// </summary>
    public static RouteHandlerBuilder UsePolicy(this RouteHandlerBuilder builder, string policyName) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        return builder.ConfigureAllMetadata(
            single => single.PolicyName = policyName,
            hub => hub.PolicyName = policyName);
    }

    /// <summary>
    /// Configures an unmanaged secret in GC-immune memory for signature verification.
    /// </summary>
    public static RouteHandlerBuilder WithSecret(this RouteHandlerBuilder builder, Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        SecretWebhookSecretResolver resolver = new(secret);
        return builder.ConfigureAllMetadata(
            single => single.SecretResolver = resolver,
            hub => hub.SecretResolver = resolver);
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
        EncryptedWebhookSecretResolver resolver = new(encryptedSecret, protector);
        return builder.ConfigureAllMetadata(
            single => single.SecretResolver = resolver,
            hub => hub.SecretResolver = resolver);
    }

    /// <summary>
    /// Explicitly permits unsigned webhook requests.
    /// </summary>
    public static RouteHandlerBuilder AllowUnsigned(this RouteHandlerBuilder builder) {
        return builder.ConfigureAllMetadata(
            single => single.RequireSignature = false,
            hub => hub.RequireSignature = false);
    }

    /// <summary>
    /// Configures whether signature verification is strictly required.
    /// </summary>
    public static RouteHandlerBuilder RequireSignature(this RouteHandlerBuilder builder, bool required = true) {
        return builder.ConfigureAllMetadata(
            single => single.RequireSignature = required,
            hub => hub.RequireSignature = required);
    }

    /// <summary>
    /// Disables inbound idempotency deduplication for this endpoint.
    /// </summary>
    public static RouteHandlerBuilder DisableIdempotency(this RouteHandlerBuilder builder) {
        return builder.ConfigureAllMetadata(
            single => single.EnforceIdempotency = false,
            hub => hub.EnforceIdempotency = false);
    }

    /// <summary>
    /// Configures the inbound idempotency deduplication window duration.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder, TimeSpan window) {
        return builder.ConfigureAllMetadata(
            single => { single.EnforceIdempotency = true; single.IdempotencyWindow = window; },
            hub => { hub.EnforceIdempotency = true; hub.IdempotencyWindow = window; });
    }

    /// <summary>
    /// Configures the maximum allowed request body size in bytes.
    /// </summary>
    public static RouteHandlerBuilder WithMaxBodySize(this RouteHandlerBuilder builder, int maxBytes) {
        Preca.ThrowIfLessThan(maxBytes, 1);
        return builder.ConfigureAllMetadata(
            single => single.MaxRequestBodyBytes = maxBytes,
            hub => hub.MaxRequestBodyBytes = maxBytes);
    }

    /// <summary>
    /// Configures the clock drift tolerance between sender and receiver.
    /// </summary>
    public static RouteHandlerBuilder WithTolerance(this RouteHandlerBuilder builder, TimeSpan tolerance) {
        return builder.ConfigureAllMetadata(
            single => single.Tolerance = tolerance,
            hub => hub.Tolerance = tolerance);
    }

    /// <summary>
    /// Configures a custom signature HTTP header name.
    /// </summary>
    public static RouteHandlerBuilder WithHeaderName(this RouteHandlerBuilder builder, string headerName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        return builder.ConfigureAllMetadata(
            single => single.HeaderName = headerName,
            hub => hub.HeaderName = headerName);
    }

    /// <summary>
    /// Configures a custom cryptographic signer instance.
    /// </summary>
    public static RouteHandlerBuilder WithSigner(this RouteHandlerBuilder builder, IWebhookSigner signer) {
        Preca.ThrowIfNull(signer);
        return builder.ConfigureAllMetadata(
            single => { single.Signer = signer; single.HeaderName = signer.HeaderName; },
            hub => { hub.Signer = signer; hub.HeaderName = signer.HeaderName; });
    }

    /// <summary>
    /// Configures event discriminator extraction from a specific HTTP header.
    /// </summary>
    public static RouteHandlerBuilder WithEventFromHeader(this RouteHandlerBuilder builder, string headerName) {
        Preca.ThrowIfNullOrWhiteSpace(headerName);
        return builder.ConfigureAllMetadata(
            single => single.EventExtractor = new HeaderEventDiscriminatorExtractor(headerName),
            hub => hub.EventExtractor = new HeaderEventDiscriminatorExtractor(headerName));
    }

    /// <summary>
    /// Configures event discriminator extraction from a root JSON property.
    /// </summary>
    public static RouteHandlerBuilder WithEventFromJsonProperty(this RouteHandlerBuilder builder, string propertyName = "type") {
        Preca.ThrowIfNullOrWhiteSpace(propertyName);
        return builder.ConfigureAllMetadata(
            single => single.EventExtractor = new JsonPropertyEventDiscriminatorExtractor(propertyName),
            hub => hub.EventExtractor = new JsonPropertyEventDiscriminatorExtractor(propertyName));
    }

    /// <summary>
    /// Configures whether unhandled incoming events should be acknowledged with 200 OK.
    /// </summary>
    public static RouteHandlerBuilder IgnoreUnhandledEvents(this RouteHandlerBuilder builder, bool ignore = true) {
        return builder.ConfigureAllMetadata(
            single => single.IgnoreUnhandledEvents = ignore,
            hub => hub.IgnoreUnhandledEvents = ignore);
    }

    /// <summary>
    /// Configures a nested JSON property path to unwrap before deserializing into the target payload model (e.g. <c>"data.object"</c>).
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="payloadPath">The dot-separated JSON property path.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static RouteHandlerBuilder WithPayloadPath(this RouteHandlerBuilder builder, string payloadPath) {
        Preca.ThrowIfNullOrWhiteSpace(payloadPath);
        return builder.ConfigureAllMetadata(
            single => single.PayloadPath = payloadPath,
            hub => hub.PayloadPath = payloadPath);
    }

    // ────────────────────────────────────────────────────────────────────────
    // INTERNAL HELPERS
    // ────────────────────────────────────────────────────────────────────────

    private static RouteHandlerBuilder ConfigureHubMetadata(this RouteHandlerBuilder builder, Action<WebhookHubMetadata> configure) {
        builder.Finally(endpointBuilder => {
            WebhookHubMetadata? metadata = endpointBuilder.Metadata.OfType<WebhookHubMetadata>().FirstOrDefault();
            if(metadata is not null) {
                configure(metadata);
            }
        });
        return builder;
    }

    private static RouteHandlerBuilder ConfigureAllMetadata(
        this RouteHandlerBuilder builder,
        Action<WebhookReceiverEndpointMetadata> configureSingle,
        Action<WebhookHubMetadata> configureHub) {

        builder.Finally(endpointBuilder => {
            WebhookReceiverEndpointMetadata? singleMetadata = endpointBuilder.Metadata.OfType<WebhookReceiverEndpointMetadata>().FirstOrDefault();
            if(singleMetadata is not null) {
                configureSingle(singleMetadata);
            }

            WebhookHubMetadata? hubMetadata = endpointBuilder.Metadata.OfType<WebhookHubMetadata>().FirstOrDefault();
            if(hubMetadata is not null) {
                configureHub(hubMetadata);
            }
        });
        return builder;
    }

    private static string ResolveEventName(Type eventType) {
        WebhookEventAttribute? attr = eventType.GetCustomAttribute<WebhookEventAttribute>();
        return attr?.Name ?? eventType.Name;
    }
}