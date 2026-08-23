using System.Collections.Frozen;
using System.Reflection;

namespace Wiaoj.Webhooks.Internal;

internal sealed class WebhookEventRegistry : IWebhookEventRegistry {
    private readonly FrozenDictionary<Type, string> _typeToName;
    private readonly FrozenDictionary<string, Type> _nameToType;
    private readonly bool _enforceExplicitNames;

    public WebhookEventRegistry(WebhookEventRegistryOptions options) {
        Preca.ThrowIfNull(options);
        this._enforceExplicitNames = options.EnforceExplicitNames;

        Dictionary<Type, string> typeToName = new(options.Mappings);
        Dictionary<string, Type> nameToType = new(StringComparer.OrdinalIgnoreCase);

        foreach((Type type, string name) in typeToName) {
            if(nameToType.TryGetValue(name, out Type? existingType)) {
                throw new InvalidOperationException(
                    $"Duplicate webhook event name detected: '{name}' is registered for both '{existingType.FullName}' and '{type.FullName}'.");
            }
            nameToType[name] = type;
        }

        this._typeToName = typeToName.ToFrozenDictionary();
        this._nameToType = nameToType.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public string GetEventName(Type eventType) {
        Preca.ThrowIfNull(eventType);

        if(this._typeToName.TryGetValue(eventType, out string? name)) {
            return name;
        }

        if(this._enforceExplicitNames) {
            throw new InvalidOperationException(
                $"Event type '{eventType.FullName}' does not have an explicit name registered and EnforceExplicitNames is enabled.");
        }

        return ResolveConventionName(eventType);
    }

    public string GetEventName<TEvent>() where TEvent : IWebhookEvent {
        return GetEventName(typeof(TEvent));
    }

    public bool TryGetEventType(string eventName, out Type? eventType) {
        return this._nameToType.TryGetValue(eventName, out eventType);
    }

    internal static string ResolveConventionName(Type type) {
        WebhookEventAttribute? attr = type.GetCustomAttribute<WebhookEventAttribute>();
        if(attr is not null) {
            return attr.Name;
        }

        string name = type.Name;
        if(name.EndsWith("WebhookEvent", StringComparison.Ordinal)) name = name[..^12];
        else if(name.EndsWith("Event", StringComparison.Ordinal)) name = name[..^5];

        return name;
    }
}