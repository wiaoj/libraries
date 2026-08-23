using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
        FakeWebhookSerializer serializer = new();
        FakeTimeProvider timeProvider = new();
        FakeWebhookEndpointResolver resolver = new();
        WebhookEventRegistry eventRegistry = new(new WebhookEventRegistryOptions());
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        resolver.Register(endpoint);

        // 1. Exponential Backoff with MaxAttempts = 2
        ExponentialBackoffOptions retryOptions = new() {
            MaxAttempts = 2,
            InitialDelay = TimeSpan.FromSeconds(1),
            Jitter = null
        };
        ExponentialBackoffPolicy retryPolicy = new(retryOptions);

        // 2. Deliverer Mock
        Queue<WebhookDeliveryResult> deliverySequence = new([
            WebhookDeliveryResult.Transient("503 Server Error", 503),
            WebhookDeliveryResult.Transient("503 Server Error", 503),
            WebhookDeliveryResult.Success(200, "OK")
        ]);

        FakeWebhookDeliverer deliverer = new(deliverySequence.ToArray());

        // 3. Pipeline
        RetryMiddleware retryMiddleware = new(retryPolicy, transport, NullLogger<RetryMiddleware>.Instance);
        WebhookPipelineRunner runner = new(
            [retryMiddleware],
            deliverer,
            new FakeTimeProvider(),
            NullLogger<WebhookPipelineRunner>.Instance);

        WebhookJobHandler jobHandler = new(store, resolver, serializer, runner, NullLogger<WebhookJobHandler>.Instance);

        // 🌟 Düzeltilen Satır: eventRegistry parametresi eklendi
        WebhookDispatcher dispatcher = new(store, transport, serializer, eventRegistry, timeProvider, NullLogger<WebhookDispatcher>.Instance);

        // ── STEP 1: DISPATCH EVENT ──
        OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();
        WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpoint.Id, @event);
        WebhookJobId jobId = handle.JobId;

        Assert.Single(transport.EnqueuedJobs);

        // ── STEP 2: WORKER PROCESSES ATTEMPT #1 (503 Error -> Schedules Retry) ──
        WebhookDeliveryJob jobAttempt1 = transport.EnqueuedJobs[0].Job;
        await jobHandler.HandleAsync(jobAttempt1);

        WebhookJobRecord? recordAfterAttempt1 = await store.GetJobAsync(jobId);
        Assert.NotNull(recordAfterAttempt1);
        Assert.Equal(WebhookJobStatus.Retrying, recordAfterAttempt1.Status);
        Assert.Single(recordAfterAttempt1.Attempts);

        // ── STEP 3: WORKER PROCESSES ATTEMPT #2 (Max Attempts Reached -> DeadLetter) ──
        Assert.Equal(2, transport.EnqueuedJobs.Count);
        WebhookDeliveryJob jobAttempt2 = transport.EnqueuedJobs[1].Job;
        await jobHandler.HandleAsync(jobAttempt2);

        WebhookJobRecord? recordDeadLettered = await store.GetJobAsync(jobId);
        Assert.NotNull(recordDeadLettered);
        Assert.Equal(WebhookJobStatus.DeadLettered, recordDeadLettered.Status);
        Assert.Equal(2, recordDeadLettered.Attempts.Count);

        IReadOnlyList<WebhookJobRecord> deadLetterList = await store.GetDeadLetteredJobsAsync(10);
        Assert.Single(deadLetterList);
        Assert.Equal(jobId, deadLetterList[0].Id);

        // ── STEP 4: OPERATOR TRIGGERS MANUAL REPLAY ──
        await dispatcher.ReplayAsync(jobId);

        WebhookJobRecord? recordReplayed = await store.GetJobAsync(jobId);
        Assert.NotNull(recordReplayed);
        Assert.Equal(WebhookJobStatus.Queued, recordReplayed.Status);

        // ── STEP 5: WORKER PROCESSES REPLAYED JOB (Success 200 OK) ──
        WebhookDeliveryJob jobReplay = transport.EnqueuedJobs[^1].Job;
        await jobHandler.HandleAsync(jobReplay);

        WebhookJobRecord? finalRecord = await store.GetJobAsync(jobId);
        Assert.NotNull(finalRecord);
        Assert.Equal(WebhookJobStatus.Delivered, finalRecord.Status);
        Assert.Equal(3, finalRecord.Attempts.Count);
        Assert.True(finalRecord.Attempts[2].IsSuccess);
    }
}