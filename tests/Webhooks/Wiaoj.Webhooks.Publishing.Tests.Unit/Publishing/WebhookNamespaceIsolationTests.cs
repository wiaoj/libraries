using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Publishing;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "NamespaceIsolation")]
public sealed class WebhookNamespaceIsolationTests {

    private static (WebhookPublisher Gateway, InMemoryWebhookSubscriptionStore Store, FakeWebhookDispatcher Dispatcher) CreateSut() {
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        return (gateway, store, dispatcher);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CROSS-TENANT DATA LEAKAGE DEFENSE (NEGATIVE & SECURITY TEST)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheCrossTenantIsolation {
        [Fact]
        public async Task PublishAsync_WhenPublishedToSpecificNamespace_DoesNotLeakToOtherNamespaces() {
            // Arrange: 
            // Tenant A ve Tenant B'nin her ikisi de "order.*" eventini dinleyen abonelik açar.
            (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

            WebhookNamespace namespaceA = new("tenant-alpha");
            WebhookNamespace namespaceB = new("tenant-beta");

            WebhookEndpointId epTenantA = new("ep-alpha-accounting");
            WebhookEndpointId epTenantB = new("ep-beta-fraud-detection");

            // Subscriptions registered under isolated namespaces
            WebhookSubscription subA = new(epTenantA, "order.*") { Namespace = namespaceA };
            WebhookSubscription subB = new(epTenantB, "order.*") { Namespace = namespaceB };

            await store.SaveSubscriptionAsync(subA, TestContext.Current.CancellationToken);
            await store.SaveSubscriptionAsync(subB, TestContext.Current.CancellationToken);

            OrderCreatedWebhookEvent orderEvent = new("ORD-ALPHA-100", 250m);

            // Act: Event strictly published under Tenant Alpha's namespace
            IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(
                namespaceA,
                orderEvent,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(handles);
            FakeWebhookDispatcher.DispatchedCall item = Assert.Single(dispatcher.Calls);

            Assert.Equal(epTenantA, item.EndpointId);
            Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == epTenantB);
        }

        [Fact]
        public async Task PublishAsync_WhenPublishedToDefaultNamespace_ExcludesTenantSpecificSubscriptions() {
            // Arrange: Bir abone default, bir abone ise "tenant-gamma" namespace'inde
            (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

            WebhookNamespace defaultNs = WebhookNamespace.Default;
            WebhookNamespace customNs = new("tenant-gamma");

            WebhookEndpointId epDefault = new("ep-global-audit");
            WebhookEndpointId epCustom = new("ep-gamma-private");

            await store.SaveSubscriptionAsync(new WebhookSubscription(epDefault, "*") { Namespace = defaultNs }, TestContext.Current.CancellationToken);
            await store.SaveSubscriptionAsync(new WebhookSubscription(epCustom, "*") { Namespace = customNs }, TestContext.Current.CancellationToken);

            // Act: Default namespace üzerinden yayınla
            IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(
                new OrderCreatedWebhookEvent("ORD-GLOBAL-1", 10m),
                TestContext.Current.CancellationToken);

            // Assert: Sadece global default aboneye gitmeli, custom kiracıya sızmamalı!
            Assert.Single(handles);
            FakeWebhookDispatcher.DispatchedCall item = Assert.Single(dispatcher.Calls);
            Assert.Equal(epDefault, item.EndpointId);
            Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == epCustom);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. MULTI-TENANT TOPOLOGY MATRIX
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheMultiTenantTopologyMatrix {
        [Fact]
        public async Task PublishAsync_UnderMultiTenantMatrix_DispatchesOnlyToMatchingNamespaceSubscribers() {
            // Arrange: 3 farklı kiracı ve her birinin birden fazla abonesi
            (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

            WebhookNamespace ns1 = new("tenant-1");
            WebhookNamespace ns2 = new("tenant-2");

            WebhookEndpointId ep1A = new("ep-t1-orders");
            WebhookEndpointId ep1B = new("ep-t1-analytics");
            WebhookEndpointId ep2A = new("ep-t2-orders");

            await store.SaveSubscriptionAsync(new WebhookSubscription(ep1A, "order.*") { Namespace = ns1 }, TestContext.Current.CancellationToken);
            await store.SaveSubscriptionAsync(new WebhookSubscription(ep1B, "*") { Namespace = ns1 }, TestContext.Current.CancellationToken);
            await store.SaveSubscriptionAsync(new WebhookSubscription(ep2A, "order.*") { Namespace = ns2 }, TestContext.Current.CancellationToken);

            // Act: Tenant-1'e event yayınla
            IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(
                ns1,
                new OrderCreatedWebhookEvent("ORD-T1", 99.90m),
                TestContext.Current.CancellationToken);

            // Assert: Tenant-1'in 2 abonesi de almalı, Tenant-2 hiç almamalı!
            Assert.Equal(2, handles.Count);
            Assert.Equal(2, dispatcher.Calls.Count);

            Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep1A);
            Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep1B);
            Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == ep2A);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. STORE-LEVEL NAMESPACE QUERY FILTERING
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheStoreScoping {
        [Fact]
        public async Task GetActiveSubscriptionsAsync_ReturnsOnlySubscriptionsMatchingRequestedNamespace() {
            InMemoryWebhookSubscriptionStore store = new();

            WebhookNamespace nsA = new("tenant-a");
            WebhookNamespace nsB = new("tenant-b");

            WebhookSubscription subA1 = new(new WebhookEndpointId("ep-a1"), "order.*") { Namespace = nsA };
            WebhookSubscription subA2 = new(new WebhookEndpointId("ep-a2"), "invoice.*") { Namespace = nsA };
            WebhookSubscription subB1 = new(new WebhookEndpointId("ep-b1"), "order.*") { Namespace = nsB };

            await store.SaveSubscriptionAsync(subA1, TestContext.Current.CancellationToken);
            await store.SaveSubscriptionAsync(subA2, TestContext.Current.CancellationToken);
            await store.SaveSubscriptionAsync(subB1, TestContext.Current.CancellationToken);

            // Act: Query by Namespace A
            IReadOnlyList<WebhookSubscription> activeInA = await store.GetActiveSubscriptionsAsync(nsA, TestContext.Current.CancellationToken);
            IReadOnlyList<WebhookSubscription> activeInB = await store.GetActiveSubscriptionsAsync(nsB, TestContext.Current.CancellationToken);

            // Assert: Store must strictly filter by namespace
            Assert.Equal(2, activeInA.Count);
            Assert.All(activeInA, s => Assert.Equal(nsA, s.Namespace));

            WebhookSubscription item = Assert.Single(activeInB);
            Assert.Equal(nsB, item.Namespace);
        }
    }
}