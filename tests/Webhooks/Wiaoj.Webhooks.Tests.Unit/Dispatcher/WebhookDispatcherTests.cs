using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.Dispatcher;

[Trait("Category", "Unit")]
[Trait("Feature", "Dispatcher")]
[Trait("Component", "Dispatcher")]
public sealed class WebhookDispatcherTests {
    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenAnyParameterIsNull() {
            // Arrange
            InMemoryWebhookStore store = new();
            InMemoryWebhookTransport transport = new();
            FakeWebhookEndpointResolver endpointResolver = new();
            WebhookPipelineRunner pipelineRunner = new([], WebhookTestFactory.CreateDeliverer(), TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance);
            SystemTextJsonSerializer<WebhookSerializerKey> serializer = new();
            WebhookEventRegistry eventRegistry = new(new WebhookEventRegistryOptions());
            FakeTimeProvider timeProvider = new();
            NullLogger<WebhookDispatcher> logger = NullLogger<WebhookDispatcher>.Instance;

            // Act & Assert: Validate guard clauses for all 8 constructor parameters
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(null!, transport, endpointResolver, pipelineRunner, serializer, eventRegistry, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, null!, endpointResolver, pipelineRunner, serializer, eventRegistry, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, null!, pipelineRunner, serializer, eventRegistry, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, endpointResolver, null!, serializer, eventRegistry, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, endpointResolver, pipelineRunner, null!, eventRegistry, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, endpointResolver, pipelineRunner, serializer, null!, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, endpointResolver, pipelineRunner, serializer, eventRegistry, null!, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, endpointResolver, pipelineRunner, serializer, eventRegistry, timeProvider, null!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. DISPATCH ASYNC EXECUTION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenDispatchingEvents {
        [Fact]
        public async Task DispatchAsync_EnqueuesJobToTransportAndSavesToStore_ReturnsValidHandle() {
            // Arrange
            InMemoryWebhookStore store = new();
            InMemoryWebhookTransport transport = new();
            FakeTimeProvider timeProvider = new();
            WebhookEventRegistry eventRegistry = new(new WebhookEventRegistryOptions());
            DateTimeOffset fixedNow = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(fixedNow);

            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(store,
                                                                               transport,
                                                                               timeProvider: timeProvider,
                                                                               eventRegistry: eventRegistry);
             
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-123");
            OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();

            // Act
            WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpointId, @event, TestContext.Current.CancellationToken);

            // Assert: Handle is valid
            Assert.False(string.IsNullOrWhiteSpace(handle.JobId.Value));

            // Assert: Store persistence verification
            WebhookJobRecord? storedJob = await store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
            Assert.NotNull(storedJob);
            Assert.Equal(endpointId, storedJob.EndpointId);
            Assert.Equal(WebhookJobStatus.Queued, storedJob.Status);
            Assert.Equal("order.created", storedJob.EventType);
            Assert.Equal(fixedNow, storedJob.CreatedAt);
            Assert.NotEmpty(storedJob.SerializedPayload);

            // Assert: Transport channel received unit of work
            bool dequeued = transport.Reader.TryRead(out WebhookDeliveryJob? job);
            Assert.True(dequeued);
            Assert.NotNull(job);
            Assert.Equal(handle.JobId, job.Id);
            Assert.Equal(endpointId, job.EndpointId);
            Assert.Equal("order.created", job.EventType);
            Assert.Same(@event, job.Payload);
        }

        [Fact]
        public async Task DispatchAsync_Throws_WhenPayloadIsNull() {
            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher();

            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                dispatcher.DispatchAsync<OrderCreatedWebhookEvent>(endpointId, null!, TestContext.Current.CancellationToken));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. ZERO-REFLECTION REPLAY ASYNC EXECUTION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenReplayingEvents {
        [Fact]
        public async Task ReplayAsync_DeserializesAndReEnqueuesJob_UsingEventRegistry() {
            // Arrange: Configure event registry with explicit type mapping for replay resolution
            InMemoryWebhookStore store = new();
            InMemoryWebhookTransport transport = new();

            WebhookEventRegistryOptions registryOptions = new();
            registryOptions.MapEvent<OrderCreatedWebhookEvent>("order.created");
            WebhookEventRegistry eventRegistry = new(registryOptions);

            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(
                store: store,
                transport: transport,
                eventRegistry: eventRegistry);

            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-456");
            const string originalJson = "{\"OrderId\":\"ORD-999\",\"Amount\":150.00}";

            // Persist a dead-lettered job in the store
            WebhookJobRecord record = new(jobId, endpointId, "order.created", originalJson, DateTimeOffset.UtcNow) {
                Status = WebhookJobStatus.DeadLettered
            };
            await store.SaveAsync(record, TestContext.Current.CancellationToken);

            // Act: Replay job
            WebhookDeliveryHandle handle = await dispatcher.ReplayAsync(jobId, TestContext.Current.CancellationToken);

            // Assert: Handle matches original JobId
            Assert.Equal(jobId, handle.JobId);

            // Assert: Store status transitioned back to Queued
            WebhookJobRecord? updated = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal(WebhookJobStatus.Queued, updated.Status);

            // Assert: Job enqueued onto transport with strongly-typed deserialized domain payload
            bool dequeued = transport.Reader.TryRead(out WebhookDeliveryJob? enqueuedJob);
            Assert.True(dequeued);
            Assert.NotNull(enqueuedJob);
            Assert.Equal(jobId, enqueuedJob.Id);
            Assert.Equal(endpointId, enqueuedJob.EndpointId);
            Assert.Equal("order.created", enqueuedJob.EventType);

            OrderCreatedWebhookEvent replayedEvent = Assert.IsType<OrderCreatedWebhookEvent>(enqueuedJob.Payload);
            Assert.Equal("ORD-999", replayedEvent.OrderId);
            Assert.Equal(150.00m, replayedEvent.Amount);
        }

        [Fact]
        public async Task ReplayAsync_ThrowsInvalidOperationException_WhenJobDoesNotExist() {
            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher();

            WebhookJobId nonExistentJobId = WebhookJobId.NewJobId();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                dispatcher.ReplayAsync(nonExistentJobId, TestContext.Current.CancellationToken));
        }
    }
}