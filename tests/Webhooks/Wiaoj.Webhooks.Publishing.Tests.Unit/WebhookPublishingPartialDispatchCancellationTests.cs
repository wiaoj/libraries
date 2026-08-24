using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "PartialCancellation")]
public sealed class WebhookPublisherPartialDispatchCancellationTests {

    [Fact]
    public async Task PublishAsync_WhenCancelledMidway_LeavesDispatchedJobsInStore_WhileCallerLosesHandles() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new(); 
        FakeWebhookDispatcher dispatcher = new(); 
        WebhookPublisher gateway =  GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);
        WebhookEndpointId ep1 = new("ep-accounting");
        WebhookEndpointId ep2 = new("ep-analytics");
        WebhookEndpointId ep3 = new("ep-crm");

        await store.SaveSubscriptionAsync(new WebhookSubscription(ep1, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep2, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep3, "order.*"));

        using CancellationTokenSource cts = new();

        // Cancel the token deterministically after exactly 2 dispatches have completed
        dispatcher.OnDispatched = _ => {
            if(dispatcher.Calls.Count == 2) {
                cts.Cancel();
            }
        };

        // Act & Assert: Method throws OperationCanceledException when attempting the 3rd dispatch
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-PARTIAL", 100m), cancellationToken: cts.Token));

        // State Inspection: Exactly 2 jobs were queued in the store before cancellation halted the loop
        Assert.Equal(2, dispatcher.Calls.Count);
    }
}