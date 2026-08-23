namespace Wiaoj.Webhooks.Tests.Unit.TestData;

/// <summary>
/// Shared test fixture event representing a canonical order creation webhook payload.
/// </summary>
/// <param name="OrderId">The unique order identifier. Defaults to <c>"ORD-1"</c>.</param>
/// <param name="Amount">The total order monetary amount. Defaults to <c>42.50</c>.</param>
[WebhookEvent("order.created")]
public sealed record OrderCreatedWebhookEvent(string OrderId, decimal Amount) : IWebhookEvent;