using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for registering clean, Minimal API-compatible inbound webhook endpoints.
/// </summary>
public static class WebhookEndpointRouteBuilderExtensions {
    /// <summary>
    /// Maps an inbound webhook endpoint with an inline delegate supporting full Dependency Injection parameter binding.
    /// </summary>
    public static RouteHandlerBuilder MapWebhook<TEvent>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Delegate handler) where TEvent : class, IWebhookEvent {
        Preca.ThrowIfNull(endpoints);
        Preca.ThrowIfNullOrWhiteSpace(pattern);
        Preca.ThrowIfNull(handler);

        WebhookReceiverEndpointMetadata metadata = new();

        return endpoints.MapPost(pattern, static () => Results.Ok())
            .WithMetadata(metadata)
            .AddEndpointFilter(new WebhookReceiverEndpointFilter<TEvent>(metadata, handler))
            .WithName($"WebhookReceiver_{typeof(TEvent).Name}")
            .WithTags("Webhooks");
    }

    /// <summary>
    /// Maps an inbound webhook endpoint routing execution to a DI-registered <see cref="IWebhookReceiverHandler{TEvent}"/>.
    /// </summary>
    public static RouteHandlerBuilder MapWebhook<TEvent>(
        this IEndpointRouteBuilder endpoints,
        string pattern) where TEvent : class, IWebhookEvent {
        Preca.ThrowIfNull(endpoints);
        Preca.ThrowIfNullOrWhiteSpace(pattern);

        WebhookReceiverEndpointMetadata metadata = new();

        return endpoints.MapPost(pattern, static () => Results.Ok())
            .WithMetadata(metadata)
            .AddEndpointFilter(new WebhookReceiverEndpointFilter<TEvent>(metadata, delegateHandler: null))
            .WithName($"WebhookReceiver_{typeof(TEvent).Name}")
            .WithTags("Webhooks");
    }
}