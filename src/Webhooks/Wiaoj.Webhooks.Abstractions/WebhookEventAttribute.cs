namespace Wiaoj.Webhooks;

/// <summary>
/// Explicitly defines the unique, canonical wire-format discriminator name for a webhook event.
/// </summary>
/// <remarks>
/// <para>
/// Decorating an <see cref="IWebhookEvent"/> implementation with this attribute ensures that the wire-format
/// event name remains immutable across internal C# refactoring operations (e.g. class renaming).
/// </para>
/// <para>
/// When this attribute is absent, the engine resolves event names using default convention fallback rules
/// or explicit registrations configured via <see cref="IWebhookEventRegistry"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [WebhookEvent("order.created")]
/// public sealed record OrderCreatedEvent(Guid OrderId, decimal Total) : IWebhookEvent;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class WebhookEventAttribute : Attribute {
    /// <summary>
    /// Gets the unique wire-format event name (e.g., <c>"order.created"</c>, <c>"invoice.paid"</c>).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEventAttribute"/> class with the specified event name.
    /// </summary>
    /// <param name="name">The canonical wire-format discriminator name for the webhook event. Cannot be <see langword="null"/>, empty, or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or consists only of whitespace.</exception>
    public WebhookEventAttribute(string name) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        this.Name = name;
    }
}