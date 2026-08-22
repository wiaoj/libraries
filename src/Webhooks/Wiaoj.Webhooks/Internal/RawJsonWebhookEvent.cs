namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Represents a pre-serialized raw JSON webhook payload used during replays and outbox forwarding.
/// Completely eliminates reflection, CLR type coupling, and deserialization overhead.
/// </summary>
internal sealed record RawJsonWebhookEvent(string EventType, string RawJson) : IWebhookEvent {
    public override string ToString() => this.RawJson;
}