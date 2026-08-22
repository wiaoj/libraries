using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.Dispatcher;

[Trait("Category", "Unit")]
[Trait("Feature", "Dispatcher")]
[Trait("Component", "Dispatcher")]
public sealed class WebhookDispatcherTests {
    private readonly SystemTextJsonSerializer<WebhookSerializerKey> _serializer = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemoryWebhookStore _store = new();

    private WebhookDispatcher CreateDispatcher(
        IWebhookStore? store = null,
        IWebhookTransport? transport = null,
        TimeProvider? timeProvider = null) {
        return new(
            store ?? this._store,
            transport ?? new InMemoryWebhookTransport(),
            this._serializer,
            timeProvider ?? this._timeProvider,
            NullLogger<WebhookDispatcher>.Instance);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenAnyParameterIsNull() {
            InMemoryWebhookStore store = new();
            InMemoryWebhookTransport transport = new();
            SystemTextJsonSerializer<WebhookSerializerKey> serializer = new();
            FakeTimeProvider timeProvider = new();
            NullLogger<WebhookDispatcher> logger = NullLogger<WebhookDispatcher>.Instance;

            // 5 parametrenin tamamının guard kontrolü:
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(null!, transport, serializer, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, null!, serializer, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, null!, timeProvider, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, serializer, null!, logger));
            Assert.ThrowsAny<ArgumentException>(() => new WebhookDispatcher(store, transport, serializer, timeProvider, null!));
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
            DateTimeOffset fixedNow = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(fixedNow);

            WebhookDispatcher dispatcher = new(store, transport, new SystemTextJsonSerializer<WebhookSerializerKey>(), timeProvider, NullLogger<WebhookDispatcher>.Instance);

            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-123");
            OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();

            // Act
            WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpointId, @event);

            // Assert: Handle is valid
            Assert.False(string.IsNullOrWhiteSpace(handle.JobId.Value));

            // Assert: Store persistence verification
            WebhookJobRecord? storedJob = await store.GetJobAsync(handle.JobId);
            Assert.NotNull(storedJob);
            Assert.Equal(endpointId, storedJob.EndpointId);
            Assert.Equal(WebhookJobStatus.Queued, storedJob.Status);
            Assert.Equal(OrderCreatedWebhookEvent.EventName, storedJob.EventType);
            Assert.Equal(fixedNow, storedJob.CreatedAt);
            Assert.NotEmpty(storedJob.SerializedPayload);

            // Assert: Transport channel received unit of work
            bool dequeued = transport.Reader.TryRead(out WebhookDeliveryJob? job);
            Assert.True(dequeued);
            Assert.NotNull(job);
            Assert.Equal(handle.JobId, job.Id);
            Assert.Equal(endpointId, job.EndpointId);
            Assert.Same(@event, job.Payload);
        }

        [Fact]
        public async Task DispatchAsync_Throws_WhenPayloadIsNull() {
            WebhookDispatcher dispatcher = new(
                new InMemoryWebhookStore(),
                new InMemoryWebhookTransport(),
                new SystemTextJsonSerializer<WebhookSerializerKey>(),
                new FakeTimeProvider(),
                NullLogger<WebhookDispatcher>.Instance);

            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                dispatcher.DispatchAsync<OrderCreatedWebhookEvent>(endpointId, null!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. ZERO-REFLECTION REPLAY ASYNC EXECUTION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenReplayingEvents {
        [Fact]
        public async Task ReplayAsync_ReEnqueuesPreSerializedPayload_WithoutDeserializationOverhead() {
            // Arrange
            InMemoryWebhookStore store = new();
            InMemoryWebhookTransport transport = new();
            WebhookDispatcher dispatcher = new(
                store,
                transport,
                new SystemTextJsonSerializer<WebhookSerializerKey>(),
                new FakeTimeProvider(),
                NullLogger<WebhookDispatcher>.Instance);

            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-456");
            const string originalJson = """{"orderId":"ORD-999","amount":150.00}""";

            // Save dead-lettered job in store
            WebhookJobRecord record = new(jobId, endpointId, "order.created", originalJson, DateTimeOffset.UtcNow) {
                Status = WebhookJobStatus.DeadLettered
            };
            await store.SaveAsync(record);

            // Act: Replay job
            WebhookDeliveryHandle handle = await dispatcher.ReplayAsync(jobId);

            // Assert: Handle matches original JobId
            Assert.Equal(jobId, handle.JobId);

            // Assert: Store status transitioned back to Queued
            WebhookJobRecord? updated = await store.GetJobAsync(jobId);
            Assert.NotNull(updated);
            Assert.Equal(WebhookJobStatus.Queued, updated.Status);

            // Assert: Job enqueued onto transport with raw pre-serialized payload
            bool dequeued = transport.Reader.TryRead(out WebhookDeliveryJob? enqueuedJob);
            Assert.True(dequeued);
            Assert.NotNull(enqueuedJob);
            Assert.Equal(jobId, enqueuedJob.Id);
            Assert.Equal(endpointId, enqueuedJob.EndpointId);
            Assert.Equal(originalJson, enqueuedJob.Payload.ToString());
        }

        [Fact]
        public async Task ReplayAsync_ThrowsInvalidOperationException_WhenJobDoesNotExist() {
            WebhookDispatcher dispatcher = new(
                new InMemoryWebhookStore(),
                new InMemoryWebhookTransport(),
                new SystemTextJsonSerializer<WebhookSerializerKey>(),
                new FakeTimeProvider(),
                NullLogger<WebhookDispatcher>.Instance);

            WebhookJobId nonExistentJobId = WebhookJobId.NewJobId();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                dispatcher.ReplayAsync(nonExistentJobId));
        }
    }
}