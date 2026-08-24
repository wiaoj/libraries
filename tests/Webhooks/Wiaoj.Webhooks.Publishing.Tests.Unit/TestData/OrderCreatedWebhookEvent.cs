namespace Wiaoj.Webhooks.Publishing.Tests.Unit.TestData;

[WebhookEvent("order.created")]
public sealed record OrderCreatedWebhookEvent(string OrderId, decimal Amount) : IWebhookEvent;