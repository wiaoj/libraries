namespace Wiaoj.Webhooks.AspNetCore.Metadata;

/// <summary>
/// Descriptor representing an event binding within a webhook hub endpoint.
/// </summary>
public sealed class WebhookHubRegistration {
    /// <summary>Gets the canonical wire-format event discriminator name.</summary>
    public string EventName { get; }

    /// <summary>Gets the target payload CLR type.</summary>
    public Type EventType { get; }

    /// <summary>Gets the optional Minimal API execution delegate.</summary>
    public Delegate? DelegateHandler { get; }

    /// <summary>Gets the optional class-based receiver handler type.</summary>
    public Type? HandlerType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookHubRegistration"/> class with an inline delegate.
    /// </summary>
    /// <param name="eventName">The wire-format event discriminator name.</param>
    /// <param name="eventType">The target payload CLR type.</param>
    /// <param name="delegateHandler">The Minimal API delegate.</param>
    public WebhookHubRegistration(string eventName, Type eventType, Delegate delegateHandler) {
        Preca.ThrowIfNullOrWhiteSpace(eventName);
        Preca.ThrowIfNull(eventType);
        Preca.ThrowIfNull(delegateHandler);

        this.EventName = eventName;
        this.EventType = eventType;
        this.DelegateHandler = delegateHandler;
        this.HandlerType = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookHubRegistration"/> class for a class-based handler.
    /// </summary>
    /// <param name="eventName">The wire-format event discriminator name.</param>
    /// <param name="eventType">The target payload CLR type.</param>
    /// <param name="handlerType">The handler type.</param>
    public WebhookHubRegistration(string eventName, Type eventType, Type? handlerType) {
        Preca.ThrowIfNullOrWhiteSpace(eventName);
        Preca.ThrowIfNull(eventType);

        this.EventName = eventName;
        this.EventType = eventType;
        this.DelegateHandler = null;
        this.HandlerType = handlerType;
    }
}