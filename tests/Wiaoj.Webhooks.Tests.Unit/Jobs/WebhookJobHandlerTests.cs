using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Jobs;

[Trait("Category", "Unit")]
[Trait("Component", "JobHandler")]
public sealed class WebhookJobHandlerTests {
    private static WebhookJobHandler CreateHandler(
        IWebhookStore? store = null,
        IWebhookEndpointResolver? resolver = null,
        ISerializer<WebhookSerializerKey>? serializer = null,
        IWebhookDeliverer? deliverer = null,
        IReadOnlyList<IWebhookMiddleware>? middleware = null) {

        deliverer ??= new FakeWebhookDeliverer();
        WebhookPipelineRunner runner = new(
            middleware ?? [],
            deliverer,
            new FakeTimeProvider(),
            NullLogger<WebhookPipelineRunner>.Instance);

        return new WebhookJobHandler(
            store ?? new InMemoryWebhookStore(),
            resolver ?? new FakeWebhookEndpointResolver().Register(WebhookTestFactory.CreateEndpoint()),
            serializer ?? new FakeWebhookSerializer(),
            runner,
            NullLogger<WebhookJobHandler>.Instance);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. ENDPOINT RESOLUTION & INGRESS
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ResolvesEndpoint_UsingJobEndpointId() {
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("acme-99");
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(endpointId);
        FakeWebhookEndpointResolver resolver = new FakeWebhookEndpointResolver().Register(endpoint);
        FakeWebhookDeliverer deliverer = new();
        WebhookDeliveryJob job = new(endpointId, "order.created", WebhookTestFactory.CreateEvent());

        WebhookJobHandler handler = CreateHandler(resolver: resolver, deliverer: deliverer);

        await handler.HandleAsync(job);

        Assert.Equal(1, resolver.CallCount);
        Assert.Same(endpoint, deliverer.ReceivedContexts[0].Endpoint);
    }

    [Fact]
    public async Task HandleAsync_ThrowsWebhookEndpointNotFoundException_WhenEndpointDoesNotResolve() {
        FakeWebhookEndpointResolver resolver = new();
        WebhookDeliveryJob job = new(WebhookTestFactory.CreateEndpointId(), "order.created", WebhookTestFactory.CreateEvent());

        WebhookJobHandler handler = CreateHandler(resolver: resolver);

        WebhookEndpointNotFoundException ex = await Assert.ThrowsAsync<WebhookEndpointNotFoundException>(
            () => handler.HandleAsync(job));

        Assert.Equal(job.EndpointId, ex.EndpointId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. PAYLOAD SERIALIZATION
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SerializesPayload_UsingConcreteRuntimeType() {
        OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();
        WebhookDeliveryJob job = new(WebhookTestFactory.CreateEndpointId(), "order.created", @event);
        FakeWebhookSerializer serializer = new();

        WebhookJobHandler handler = CreateHandler(serializer: serializer);

        await handler.HandleAsync(job);

        Assert.Equal(@event, serializer.LastSerializedCall?.Value);
        Assert.Equal(typeof(OrderCreatedWebhookEvent), serializer.LastSerializedCall?.Type);
    }

    [Fact]
    public async Task HandleAsync_PassesSerializedPayload_IntoDeliveryContext() {
        const string serialized = """{"custom":true}""";
        WebhookDeliveryJob job = new(WebhookTestFactory.CreateEndpointId(), "order.created", WebhookTestFactory.CreateEvent());
        FakeWebhookDeliverer deliverer = new();
        FakeWebhookSerializer serializer = new(serialized);

        WebhookJobHandler handler = CreateHandler(deliverer: deliverer, serializer: serializer);

        await handler.HandleAsync(job);

        Assert.Equal(serialized, deliverer.ReceivedContexts[0].SerializedPayload);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. STORE PERSISTENCE & LIFECYCLE STATUS (DEAD-LETTERING)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WhenDeliverySucceeds_UpdatesJobStatusToDelivered_AndRecordsAttempt() {
        InMemoryWebhookStore store = new();
        WebhookDeliveryResult successResult = WebhookTestFactory.CreateSuccessResult(200);
        FakeWebhookDeliverer deliverer = new(successResult);

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
        WebhookJobRecord record = new(jobId, endpointId, "order.created", "{}", DateTimeOffset.UtcNow);
        await store.SaveAsync(record);

        WebhookDeliveryJob job = new(jobId, endpointId, "order.created", WebhookTestFactory.CreateEvent());
        WebhookJobHandler handler = CreateHandler(store: store, deliverer: deliverer);

        // Act
        WebhookDeliveryAttempt attempt = await handler.HandleAsync(job);

        // Assert
        Assert.True(attempt.IsSuccess);
        WebhookJobRecord? updated = await store.GetJobAsync(jobId);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.Delivered, updated.Status);
        Assert.Single(updated.Attempts);
        Assert.True(updated.Attempts[0].IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_WhenTransientFailureOccurs_UpdatesJobStatusToRetrying() {
        InMemoryWebhookStore store = new();
        WebhookDeliveryResult failureResult = WebhookTestFactory.CreateTransientFailureResult("503", 503);
        FakeWebhookDeliverer deliverer = new(failureResult);

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
        WebhookJobRecord record = new(jobId, endpointId, "order.created", "{}", DateTimeOffset.UtcNow);
        await store.SaveAsync(record);

        WebhookDeliveryJob job = new(jobId, endpointId, "order.created", WebhookTestFactory.CreateEvent());
        WebhookJobHandler handler = CreateHandler(store: store, deliverer: deliverer);

        // Act
        WebhookDeliveryAttempt attempt = await handler.HandleAsync(job);

        // Assert
        Assert.False(attempt.IsSuccess);
        WebhookJobRecord? updated = await store.GetJobAsync(jobId);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.Retrying, updated.Status);
        Assert.Single(updated.Attempts);
    }

    [Fact]
    public async Task HandleAsync_WhenPermanentFailureOrDeadLettered_UpdatesJobStatusToDeadLettered() {
        InMemoryWebhookStore store = new();
        WebhookDeliveryResult permanentFailure = WebhookTestFactory.CreatePermanentFailureResult("404 Not Found", 404);
        FakeWebhookDeliverer deliverer = new(permanentFailure);

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
        WebhookJobRecord record = new(jobId, endpointId, "order.created", "{}", DateTimeOffset.UtcNow);
        await store.SaveAsync(record);

        WebhookDeliveryJob job = new(jobId, endpointId, "order.created", WebhookTestFactory.CreateEvent());
        WebhookJobHandler handler = CreateHandler(store: store, deliverer: deliverer);

        // Act
        WebhookDeliveryAttempt attempt = await handler.HandleAsync(job);

        // Assert
        Assert.False(attempt.IsSuccess);
        WebhookJobRecord? updated = await store.GetJobAsync(jobId);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.DeadLettered, updated.Status);
        Assert.Single(updated.Attempts);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. CONSTRUCTOR GUARD CLAUSES
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_Throws_WhenAnyParameterIsNull() {
        InMemoryWebhookStore store = new();
        FakeWebhookEndpointResolver resolver = new();
        FakeWebhookSerializer serializer = new();
        WebhookPipelineRunner runner = new([], new FakeWebhookDeliverer(), new FakeTimeProvider(), NullLogger<WebhookPipelineRunner>.Instance);
        NullLogger<WebhookJobHandler> logger = NullLogger<WebhookJobHandler>.Instance;

        Assert.ThrowsAny<ArgumentException>(() => new WebhookJobHandler(null!, resolver, serializer, runner, logger));
        Assert.ThrowsAny<ArgumentException>(() => new WebhookJobHandler(store, null!, serializer, runner, logger));
        Assert.ThrowsAny<ArgumentException>(() => new WebhookJobHandler(store, resolver, null!, runner, logger));
        Assert.ThrowsAny<ArgumentException>(() => new WebhookJobHandler(store, resolver, serializer, null!, logger));
        Assert.ThrowsAny<ArgumentException>(() => new WebhookJobHandler(store, resolver, serializer, runner, null!));
    }
}