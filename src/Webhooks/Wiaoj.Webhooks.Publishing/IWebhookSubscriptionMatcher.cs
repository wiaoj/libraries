namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Evaluates whether a webhook subscription matches an outbound event and its optional payload criteria.
/// </summary>
public interface IWebhookSubscriptionMatcher {
    /// <summary>
    /// Evaluates whether the given subscription matches the dispatched event name and payload.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event payload.</typeparam>
    /// <param name="subscription">The subscription registration to evaluate.</param>
    /// <param name="eventName">The canonical wire-format event name.</param>
    /// <param name="payload">The strongly-typed domain event payload instance.</param>
    /// <returns><see langword="true"/> if the subscription matches; otherwise, <see langword="false"/>.</returns>
    bool Matches<TEvent>(WebhookSubscription subscription, string eventName, TEvent payload) where TEvent : IWebhookEvent;

    /// <summary>
    /// Evaluates whether a raw pattern string matches the given event name.
    /// </summary>
    /// <param name="pattern">The pattern declared on the subscription (e.g. <c>"order.*"</c>, <c>"*"</c>, <c>"order.created"</c>).</param>
    /// <param name="eventName">The canonical event name being published.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    bool Matches(string pattern, string eventName);
}