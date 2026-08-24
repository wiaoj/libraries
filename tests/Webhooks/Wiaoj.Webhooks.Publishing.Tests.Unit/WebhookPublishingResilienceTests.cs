using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Publishing.Tests.Unit.TestData;
using Wiaoj.Webhooks.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "Resilience")]
public sealed class WebhookPublisherResilienceTests {

    [Fact]
    public async Task PublishAsync_WhenCancellationTokenIsCancelled_AbortsExecution() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store); 

        await store.SaveSubscriptionAsync(new WebhookSubscription(new WebhookEndpointId("ep-1"), "*"));

        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert: Pre-cancelled token must abort operation
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-1", 10m), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task PublishAsync_WhenSubscriptionIsDisabledAtRuntime_ExcludesDisabledEndpointImmediately() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new(); 
        FakeWebhookDispatcher dispatcher = new(); 
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId endpointId = new("ep-dynamic-toggle");
        WebhookSubscription subscription = new(endpointId, "order.*") { IsEnabled = true };
        await store.SaveSubscriptionAsync(subscription);

        // Act 1: Active subscription receives dispatch
        IReadOnlyList<WebhookDeliveryHandle> firstHandles = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-1", 10m));
        Assert.Single(firstHandles);
        Assert.Single(dispatcher.Calls);

        // Act 2: Disable subscription dynamically
        subscription.IsEnabled = false;
        await store.SaveSubscriptionAsync(subscription);

        IReadOnlyList<WebhookDeliveryHandle> secondHandles = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-2", 20m));

        // Assert: Disabled subscription receives no new dispatches
        Assert.Empty(secondHandles);
        Assert.Single(dispatcher.Calls);
    }
}