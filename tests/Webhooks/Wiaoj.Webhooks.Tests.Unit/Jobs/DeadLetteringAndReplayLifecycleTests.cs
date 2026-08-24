using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Jobs;

[Trait("Category", "Unit")]
[Trait("Feature", "DeadLettering")]
[Trait("Component", "Lifecycle")]
public sealed class DeadLetteringAndReplayLifecycleTests {
    [Fact]
    public async Task FullOutboundLifecycle_WhenRetriesExhausted_DeadLettersJob_AndAllowsManualReplay() {
        // Arrange
        InMemoryWebhookStore store = new();
        FakeWebhookTransport transport = new();
        SystemTextJsonSerializer<WebhookSerializerKey> serializer = new();
        FakeWebhookEndpointResolver resolver = new();

        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        resolver.Register(endpoint);

        // 1. Exponential Backoff with MaxAttempts = 2
        ExponentialBackoffOptions retryOptions = new() {
            MaxAttempts = 2,
            InitialDelay = TimeSpan.FromSeconds(1),
            Jitter = null
        };
        ExponentialBackoffPolicy retryPolicy = new(retryOptions);

        // 2. Mock Delivery Sequence: 503 -> 503 -> 200 (Success on replay)
        Queue<WebhookDeliveryResult> deliverySequence = new([
            WebhookDeliveryResult.Transient("503 Server Error", 503),
            WebhookDeliveryResult.Transient("503 Server Error", 503),
            WebhookDeliveryResult.Success(200, "OK")
        ]);

        FakeWebhookDeliverer deliverer = new([.. deliverySequence]);

        // 3. Pipeline Construction
        RetryMiddleware retryMiddleware = new(retryPolicy, transport, NullLogger<RetryMiddleware>.Instance);
        WebhookPipelineRunner runner = new(
            [retryMiddleware],
            deliverer,
            new FakeTimeProvider(),
            NullLogger<WebhookPipelineRunner>.Instance);

        WebhookJobHandler jobHandler = new(store, resolver, serializer, runner, NullLogger<WebhookJobHandler>.Instance);

        WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(
            store: store,
            transport: transport,
            serializer: serializer,
            endpointResolver: resolver);

        // Step 1: Initial Event Dispatch
        OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();
        WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpoint.Id, @event, TestContext.Current.CancellationToken);
        WebhookJobId jobId = handle.JobId;

        Assert.Single(transport.EnqueuedJobs);

        // Step 2: Worker processes Attempt #1 (503 Error -> Schedules Retry)
        WebhookDeliveryJob jobAttempt1 = transport.EnqueuedJobs[0].Job;
        await jobHandler.HandleAsync(jobAttempt1, TestContext.Current.CancellationToken);

        WebhookJobRecord? recordAfterAttempt1 = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(recordAfterAttempt1);
        Assert.Equal(WebhookJobStatus.Retrying, recordAfterAttempt1.Status);
        Assert.Single(recordAfterAttempt1.Attempts);
        Assert.False(recordAfterAttempt1.Attempts[0].IsReplay);

        // Step 3: Worker processes Attempt #2 (Max attempts reached -> Transitions to DeadLettered)
        Assert.Equal(2, transport.EnqueuedJobs.Count);
        WebhookDeliveryJob jobAttempt2 = transport.EnqueuedJobs[1].Job;
        await jobHandler.HandleAsync(jobAttempt2, TestContext.Current.CancellationToken);

        WebhookJobRecord? recordDeadLettered = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(recordDeadLettered);
        Assert.Equal(WebhookJobStatus.DeadLettered, recordDeadLettered.Status);
        Assert.Equal(2, recordDeadLettered.Attempts.Count);
        Assert.False(recordDeadLettered.Attempts[1].IsReplay);

        IReadOnlyList<WebhookJobRecord> deadLetterList = await store.GetDeadLetteredJobsAsync(10, TestContext.Current.CancellationToken);
        Assert.Single(deadLetterList);
        Assert.Equal(jobId, deadLetterList[0].Id);

        // Step 4: Operator triggers manual replay of the dead-lettered job
        await dispatcher.ReplayAsync(jobId, TestContext.Current.CancellationToken);

        WebhookJobRecord? recordReplayed = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(recordReplayed);
        Assert.Equal(WebhookJobStatus.Queued, recordReplayed.Status);

        // Step 5: Worker processes the replayed job (Delivery succeeds with 200 OK)
        WebhookDeliveryJob jobReplay = transport.EnqueuedJobs[^1].Job;
        WebhookDeliveryAttempt attempt3 = await jobHandler.HandleAsync(jobReplay, TestContext.Current.CancellationToken);

        WebhookJobRecord? finalRecord = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(finalRecord);
        Assert.Equal(WebhookJobStatus.Delivered, finalRecord.Status);
        Assert.Equal(3, finalRecord.Attempts.Count);
        Assert.True(finalRecord.Attempts[2].IsSuccess);

        // Assert: Replayed attempt must explicitly be flagged as replay in both memory and store
        Assert.True(attempt3.IsReplay);
        Assert.True(finalRecord.Attempts[2].IsReplay);
    }

    [Fact]
    public async Task ReplayAsync_WhenReplayingAlreadyDeliveredJob_BypassesIdempotencyAndDeliversAgain() {
        // Arrange
        InMemoryWebhookStore store = new();
        FakeWebhookTransport transport = new();
        FakeWebhookEndpointResolver resolver = new();
        InMemoryIdempotencyStore idempotencyStore = new();
        DefaultIdempotencyKeyGenerator keyGenerator = new();

        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        resolver.Register(endpoint);

        FakeWebhookDeliverer deliverer = new(
            WebhookDeliveryResult.Success(200, "OK"),
            WebhookDeliveryResult.Success(200, "OK"));

        IdempotencyOptions idempotencyOptions = new() {
            Window = TimeSpan.FromHours(24)
        };

        IdempotencyMiddleware idempotencyMiddleware = new(
            idempotencyStore,
            keyGenerator,
            idempotencyOptions,
            NullLogger<IdempotencyMiddleware>.Instance);

        WebhookPipelineRunner runner = new(
            [idempotencyMiddleware],
            deliverer,
            new FakeTimeProvider(),
            NullLogger<WebhookPipelineRunner>.Instance);

        WebhookJobHandler jobHandler = new(
            store,
            resolver,
            new SystemTextJsonSerializer<WebhookSerializerKey>(),
            runner,
            NullLogger<WebhookJobHandler>.Instance);

        WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(
            store: store,
            transport: transport,
            endpointResolver: resolver);

        // Act - Step 1: Initial dispatch and successful delivery
        OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent("ORD-REPLAY-1", 100m);
        WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpoint.Id, @event, TestContext.Current.CancellationToken);

        WebhookDeliveryJob firstJob = transport.EnqueuedJobs[0].Job;
        WebhookDeliveryAttempt attempt1 = await jobHandler.HandleAsync(firstJob, TestContext.Current.CancellationToken);

        Assert.True(attempt1.IsSuccess);
        Assert.False(attempt1.IsReplay);
        Assert.Single(deliverer.ReceivedContexts);

        WebhookJobRecord? record1 = await store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
        Assert.NotNull(record1);
        Assert.Equal(WebhookJobStatus.Delivered, record1.Status);
        Assert.False(record1.Attempts[0].IsReplay);

        // Act - Step 2: Trigger manual replay of the already delivered job
        await dispatcher.ReplayAsync(handle.JobId, TestContext.Current.CancellationToken);

        Assert.Equal(2, transport.EnqueuedJobs.Count);
        WebhookDeliveryJob replayedJob = transport.EnqueuedJobs[1].Job;

        // Act - Step 3: Handle the replayed job
        WebhookDeliveryAttempt attempt2 = await jobHandler.HandleAsync(replayedJob, TestContext.Current.CancellationToken);

        // Assert: Must deliver to the target a second time and produce Delivered result
        Assert.Equal(2, deliverer.ReceivedContexts.Count);
        Assert.IsType<WebhookDeliveryResult.Delivered>(attempt2.Result);
        Assert.True(attempt2.IsReplay);

        WebhookJobRecord? finalRecord = await store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
        Assert.NotNull(finalRecord);
        Assert.Equal(2, finalRecord.Attempts.Count);
        Assert.True(finalRecord.Attempts[1].IsReplay);
    }
}