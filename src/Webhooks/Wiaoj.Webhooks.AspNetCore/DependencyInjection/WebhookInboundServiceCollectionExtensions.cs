using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.AspNetCore;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for registering inbound webhook receiver policies and services in DI.
/// </summary>
public static class WebhookInboundServiceCollectionExtensions {
    /// <summary>
    /// Registers inbound webhook receiver services and named policies.
    /// </summary>
    public static IServiceCollection AddInboundWebhooks(this IServiceCollection services, Action<WebhookInboundBuilder>? configure = null) {
        Preca.ThrowIfNull(services);

        services.AddOptions<WebhookInboundOptions>();

        WebhookInboundBuilder builder = new(services);
        configure?.Invoke(builder);

        return services;
    }

    /// <summary>
    /// Registers a class-based <see cref="IWebhookReceiverHandler{TEvent}"/> implementation in the service collection.
    /// </summary>
    public static IServiceCollection AddWebhookHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : class, IWebhookEvent
        where THandler : class, IWebhookReceiverHandler<TEvent> {
        Preca.ThrowIfNull(services);
        services.TryAddScoped<IWebhookReceiverHandler<TEvent>, THandler>();
        return services;
    }
}