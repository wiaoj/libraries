using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Serialization.Memory;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "PatternOverlapMatrix")]
public sealed class RouterPatternOverlapMatrixTests {

    [Fact]
    public async Task PublishAsync_WhenUniversalWildcardRegisteredBeforeExactRule_DispatchesConcreteEventOnce() {
        // Arrange: Universal wildcard (*) registered first, exact rule registered second
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId endpointId = new("crm-endpoint");
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "order.created"));

        OrderCreatedWebhookEvent originalPayload = new("ORD-100", 49.90m);

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(originalPayload);

        // Assert: Exactly one dispatch, original payload and event name preserved without mutation
        Assert.Single(handles);
        Assert.Single(dispatcher.Calls);

        FakeWebhookDispatcher.DispatchedCall call = dispatcher.Calls[0];
        Assert.Equal(endpointId, call.EndpointId);
        Assert.Same(originalPayload, call.Payload);
    }

    [Fact]
    public async Task PublishAsync_WhenExactRuleRegisteredBeforeUniversalWildcard_DispatchesConcreteEventOnce() {
        // Arrange: Exact rule registered first, universal wildcard (*) registered second
        InMemoryWebhookSubscriptionStore store = new(); 
        FakeWebhookDispatcher dispatcher = new(); 
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId endpointId = new("billing-endpoint");
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "order.created"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "*"));

        OrderCreatedWebhookEvent originalPayload = new("ORD-200", 99.00m);

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(originalPayload);

        // Assert: Registration order does not cause duplicate dispatches
        Assert.Single(handles);
        Assert.Single(dispatcher.Calls);
        Assert.Equal(endpointId, dispatcher.Calls[0].EndpointId);
        Assert.Same(originalPayload, dispatcher.Calls[0].Payload);
    }

    [Fact]
    public async Task PublishAsync_WhenQuadrupleOverlappingRulesMatchSameEndpoint_ExecutesSingleDispatch() {
        // Arrange: Same endpoint registered with 4 overlapping rules (*, order.*, *.created, order.created)
        InMemoryWebhookSubscriptionStore store = new(); 
        FakeWebhookDispatcher dispatcher = new();  
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId endpointId = new("data-lake");
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "*.created"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(endpointId, "order.created"));

        OrderCreatedWebhookEvent originalPayload = new("ORD-4X", 500m);

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(originalPayload);

        // Assert: All 4 matching rules collapse into a single dispatch
        Assert.Single(handles);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task PublishAsync_WhenOnlyUniversalWildcardIsRegistered_DispatchesAllEvents() {
        // Arrange: Endpoint listens strictly to '*'
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId auditEndpoint = new("global-audit-log");
        await store.SaveSubscriptionAsync(new WebhookSubscription(auditEndpoint, "*"));

        // Act: Publish two distinct event types
        IReadOnlyList<WebhookDeliveryHandle> handles1 = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-1", 10m));
        IReadOnlyList<WebhookDeliveryHandle> handles2 = await gateway.PublishAsync(new InvoicePaidWebhookEvent("INV-1", 10m));

        // Assert: Both distinct event types are dispatched to the universal listener
        Assert.Single(handles1);
        Assert.Single(handles2);
        Assert.Equal(2, dispatcher.Calls.Count);
        Assert.Equal("ORD-1", ((OrderCreatedWebhookEvent)dispatcher.Calls[0].Payload).OrderId);
        Assert.Equal("INV-1", ((InvoicePaidWebhookEvent)dispatcher.Calls[1].Payload).InvoiceId);
    }

    [Fact]
    public async Task PublishAsync_CrossEventRouting_MatchesPrefixAndSuffixCorrectlyWithoutCrossPollution() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId orderPrefixEndpoint = new("order-service");
        WebhookEndpointId createdSuffixEndpoint = new("created-listener");

        await store.SaveSubscriptionAsync(new WebhookSubscription(orderPrefixEndpoint, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(createdSuffixEndpoint, "*.created"));

        // Act 1: Publish order.deleted (Matches order.*, but NOT *.created)
        await gateway.PublishAsync(new OrderDeletedWebhookEvent("ORD-DEL-1"));

        // Act 2: Publish invoice.created (Matches *.created, but NOT order.*)
        await gateway.PublishAsync(new InvoiceCreatedWebhookEvent("INV-CREATED-1", 100m));

        // Assert: Verify strict routing isolation
        Assert.Equal(2, dispatcher.Calls.Count);
        Assert.Equal(orderPrefixEndpoint, dispatcher.Calls[0].EndpointId);
        Assert.Equal(createdSuffixEndpoint, dispatcher.Calls[1].EndpointId);
    }

    [Fact]
    public async Task PublishAsync_MultiEndpointMatrix_DispatchesOnlyToMatchingSubsets() {
        // Arrange: 5 distinct endpoints with mixed overlapping and non-overlapping patterns
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId ep1 = new("ep-universal");
        WebhookEndpointId ep2 = new("ep-order-all");
        WebhookEndpointId ep3 = new("ep-created-all");
        WebhookEndpointId ep4 = new("ep-exact");
        WebhookEndpointId ep5 = new("ep-payment-all");

        await store.SaveSubscriptionAsync(new WebhookSubscription(ep1, "*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep2, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep3, "*.created"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep4, "order.created"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep5, "payment.*"));

        // Act: Publish order.created
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-MATRIX", 75m));

        // Assert: Endpoints 1, 2, 3, and 4 must receive dispatch; Endpoint 5 must be excluded
        Assert.Equal(4, handles.Count);
        Assert.Equal(4, dispatcher.Calls.Count);

        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep1);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep2);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep3);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep4);
        Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == ep5);
    }

    [WebhookEvent("invoice.paid")]
    public sealed record InvoicePaidWebhookEvent(string InvoiceId, decimal Amount) : IWebhookEvent;

    [WebhookEvent("invoice.created")]
    public sealed record InvoiceCreatedWebhookEvent(string InvoiceId, decimal Amount) : IWebhookEvent;

    [WebhookEvent("order.deleted")]
    public sealed record OrderDeletedWebhookEvent(string OrderId) : IWebhookEvent;
}