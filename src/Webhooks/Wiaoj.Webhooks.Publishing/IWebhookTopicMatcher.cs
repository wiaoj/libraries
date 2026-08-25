namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Evaluates whether an event name matches a subscription topic pattern.
/// </summary>
public interface IWebhookTopicMatcher {
    /// <summary>
    /// Evaluates whether the pattern matches the canonical wire-format event name.
    /// </summary>
    /// <param name="pattern">The pattern declared on the subscription (e.g. <c>"order.*"</c>, <c>"*"</c>, <c>"order.created"</c>).</param>
    /// <param name="eventName">The canonical wire-format event name being published.</param>
    /// <returns><see langword="true"/> if matched; otherwise, <see langword="false"/>.</returns>
    bool Matches(string pattern, string eventName);
}