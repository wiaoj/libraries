namespace Wiaoj.Webhooks.Tests.Unit.TestData;

// Moved out of WebhookDispatcherTests.cs so every test class can share the same fixture event.
public sealed record OrderCreatedWebhookEvent : IWebhookEvent {
    public static string EventName => "order.created";
}