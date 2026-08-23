namespace Wiaoj.Webhooks;

/// <summary>
/// Immutable, thread-safe registry mapping runtime CLR event types to wire-format event names and vice versa.
/// </summary>
public interface IWebhookEventRegistry {
    /// <summary>
    /// Gets the wire-format event name for the specified CLR event type.
    /// </summary>
    /// <param name="eventType">The CLR type of the webhook event.</param>
    /// <returns>The resolved canonical event name.</returns>
    string GetEventName(Type eventType);

    /// <summary>
    /// Gets the wire-format event name for the generic event type with zero allocation.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The resolved canonical event name.</returns>
    string GetEventName<TEvent>() where TEvent : IWebhookEvent;

    /// <summary>
    /// Attempts to resolve the CLR type corresponding to a wire-format event name.
    /// </summary>
    /// <param name="eventName">The wire-format event name to look up.</param>
    /// <param name="eventType">When this method returns, contains the matching CLR type if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a mapping was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetEventType(string eventName, out Type? eventType);
}