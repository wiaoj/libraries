using System.Reflection;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// High-performance static metadata cache resolving event discriminator names with zero runtime allocation.
/// </summary>
internal static class WebhookEventMetadata<TEvent> where TEvent : IWebhookEvent {
    /// <summary>
    /// Gets the resolved wire-format event name.
    /// </summary>
    public static readonly string Name = ResolveName();

    private static string ResolveName() {
        WebhookEventAttribute? attr = typeof(TEvent).GetCustomAttribute<WebhookEventAttribute>();
        if(attr is not null) {
            return attr.Name;
        }

        // Convention fallback: OrderCreatedWebhookEvent -> "order.created"
        string typeName = typeof(TEvent).Name;
        if(typeName.EndsWith("WebhookEvent", StringComparison.Ordinal)) {
            typeName = typeName[..^12];
        }
        else if(typeName.EndsWith("Event", StringComparison.Ordinal)) {
            typeName = typeName[..^5];
        }

        return typeName;
    }
}