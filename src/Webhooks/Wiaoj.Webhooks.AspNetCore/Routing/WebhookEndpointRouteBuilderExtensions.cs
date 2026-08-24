using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for registering Minimal API inbound webhook endpoints and multi-event hubs.
/// </summary>
public static class WebhookEndpointRouteBuilderExtensions {
    /// <summary>
    /// Maps a multi-event webhook hub endpoint on the specified path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route URL pattern (e.g. <c>"/api/webhooks/github"</c>).</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> for chaining event bindings and policy options.</returns>
    public static RouteHandlerBuilder MapWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern) {
        Preca.ThrowIfNull(endpoints);
        Preca.ThrowIfNullOrWhiteSpace(pattern);

        WebhookHubMetadata metadata = new();

        return endpoints.MapPost(pattern, static () => Results.Ok())
            .WithMetadata(metadata)
            .AddEndpointFilter(new WebhookHubEndpointFilter(metadata))
            .WithName($"WebhookHub_{pattern.Replace('/', '_').Trim('_')}")
            .WithTags("Webhooks");
    }

    /// <summary>
    /// Maps a dedicated 1-to-1 inbound webhook endpoint with an inline delegate.
    /// </summary>
    /// <typeparam name="TEvent">The target payload model type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route URL pattern.</param>
    /// <param name="handler">The Minimal API delegate.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> for chaining options.</returns>
    public static RouteHandlerBuilder MapWebhook<TEvent>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Delegate handler) where TEvent : class {
        Preca.ThrowIfNull(endpoints);
        Preca.ThrowIfNullOrWhiteSpace(pattern);
        Preca.ThrowIfNull(handler);

        string eventName = ResolveEventName(typeof(TEvent));

        return endpoints.MapWebhook(pattern)
            .On<TEvent>(eventName, handler);
    }

    private static string ResolveEventName(Type eventType) {
        WebhookEventAttribute? attr = eventType.GetCustomAttribute<WebhookEventAttribute>();
        return attr?.Name ?? eventType.Name;
    }
}