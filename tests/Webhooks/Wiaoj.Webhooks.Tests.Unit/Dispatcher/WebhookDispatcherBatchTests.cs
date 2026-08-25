using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Dispatcher;

[Trait("Category", "Unit")]
[Trait("Feature", "Dispatcher")]
[Trait("Component", "BatchDispatching")]
public sealed class WebhookDispatcherBatchTests {

    private static (WebhookDispatcher Dispatcher, InMemoryWebhookStore Store, FakeWebhookTransport Transport) CreateSut() {
        InMemoryWebhookStore store = new();
        FakeWebhookTransport transport = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        return (dispatcher, store, transport);
    }

    [Fact]
    public async Task DispatchBatchAsync_PersistsAllJobsInSingleBatch_AndEnqueuesToTransport() {
        // Arrange
        (WebhookDispatcher dispatcher, InMemoryWebhookStore store, FakeWebhookTransport transport) = CreateSut();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-bulk-1");

        // 10 distinct domain events
        OrderCreatedWebhookEvent[] events = Enumerable.Range(1, 10)
            .Select(i => new OrderCreatedWebhookEvent($"ORD-BULK-{i}", i * 25.50m))
            .ToArray();

        // Act: Dispatch 10 events as a single batch
        IReadOnlyList<WebhookDeliveryHandle> handles = await dispatcher.DispatchBatchAsync(
            endpointId,
            events,
            TestContext.Current.CancellationToken);

        // Assert: 10 distinct handles returned
        Assert.Equal(10, handles.Count);
        Assert.Equal(10, handles.Select(h => h.JobId).Distinct().Count());

        // Assert: All 10 jobs are saved in the store
        IReadOnlyList<WebhookJobRecord> storedJobs = await store.GetHistoryByEndpointAsync(endpointId, TestContext.Current.CancellationToken);
        Assert.Equal(10, storedJobs.Count);

        // Assert: Transport received 10 jobs
        Assert.Equal(10, transport.EnqueuedJobs.Count);
        for(int i = 0; i < 10; i++) {
            Assert.Equal(handles[i].JobId, transport.EnqueuedJobs[i].Job.Id);
            Assert.Equal(endpointId, transport.EnqueuedJobs[i].Job.EndpointId);
        }

        // Assert: All 10 jobs must carry the exact same, non-null BatchId
        string? firstBatchId = storedJobs[0].BatchId;
        Assert.False(string.IsNullOrWhiteSpace(firstBatchId));
        Assert.All(storedJobs, job => Assert.Equal(firstBatchId, job.BatchId));
    }

    [Fact]
    public async Task DispatchBatchAsync_WithCustomPartitionKeySelector_AssignsCorrectPartitionKeys() {
        (WebhookDispatcher dispatcher, InMemoryWebhookStore store, FakeWebhookTransport transport) = CreateSut();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-partitioned");

        OrderCreatedWebhookEvent[] events = [
            new("ORD-A1", 100m),
            new("ORD-A2", 200m),
            new("ORD-B1", 300m)
        ];

        // Act: Custom partition selector grouping by prefix
        IReadOnlyList<WebhookDeliveryHandle> handles = await dispatcher.DispatchBatchAsync(
            endpointId,
            events,
            partitionKeySelector: e => e.OrderId.StartsWith("ORD-A") ? "partition-group-A" : "partition-group-B",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, handles.Count);

        // Assert: Partition keys match selector logic
        Assert.Equal("partition-group-A", transport.EnqueuedJobs[0].Job.PartitionKey.Value);
        Assert.Equal("partition-group-A", transport.EnqueuedJobs[1].Job.PartitionKey.Value);
        Assert.Equal("partition-group-B", transport.EnqueuedJobs[2].Job.PartitionKey.Value);
    }

    [Fact]
    public async Task DispatchBatchAsync_WhenPayloadsListIsEmpty_ReturnsEmptyHandlesImmediately() {
        (WebhookDispatcher dispatcher, InMemoryWebhookStore store, FakeWebhookTransport transport) = CreateSut();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-empty");

        IReadOnlyList<WebhookDeliveryHandle> handles = await dispatcher.DispatchBatchAsync(
            endpointId,
            Array.Empty<OrderCreatedWebhookEvent>(),
            TestContext.Current.CancellationToken);

        Assert.Empty(handles);
        Assert.Empty(transport.EnqueuedJobs);
    }

    [Fact]
    public async Task DispatchBatchAsync_ThrowsArgumentNullException_WhenPayloadsCollectionIsNull() {
        (WebhookDispatcher dispatcher, _, _) = CreateSut();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();

        await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
            dispatcher.DispatchBatchAsync<OrderCreatedWebhookEvent>(endpointId, null!, TestContext.Current.CancellationToken));
    }
}