using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Serialization.SystemTextJson;
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
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        resolver.Register(endpoint);

        // 1. Exponential Backoff with MaxAttempts = 2 (1 initial attempt + 1 retry attempt)
        ExponentialBackoffOptions retryOptions = new() {
            MaxAttempts = 2,
            InitialDelay = TimeSpan.FromSeconds(1),
            Jitter = null
        };
        ExponentialBackoffPolicy retryPolicy = new(retryOptions);

        // 2. Deliverer Mock: Returns 503 for first 2 attempts, then 200 OK when replayed
        Queue<WebhookDeliveryResult> deliverySequence = new([
            WebhookDeliveryResult.Transient("503 Server Error", 503), // Attempt #1 -> Retry scheduled
            WebhookDeliveryResult.Transient("503 Server Error", 503), // Attempt #2 -> Retries exhausted -> DeadLetter
            WebhookDeliveryResult.Success(200, "OK")                  // Replay attempt -> Success!
        ]);

        FakeWebhookDeliverer deliverer = new(deliverySequence.ToArray());

        // 3. Pipeline: Configured with RetryMiddleware
        RetryMiddleware retryMiddleware = new(retryPolicy, transport, NullLogger<RetryMiddleware>.Instance);
        WebhookPipelineRunner runner = new(
            [retryMiddleware],
            deliverer,
            new FakeTimeProvider(),
            NullLogger<WebhookPipelineRunner>.Instance);

        WebhookJobHandler jobHandler = new(store, resolver, serializer, runner, NullLogger<WebhookJobHandler>.Instance);
        WebhookDispatcher dispatcher = new(store, transport, serializer, timeProvider, NullLogger<WebhookDispatcher>.Instance);

        // ── STEP 1: DISPATCH EVENT ───────────────────────────────────────────
        OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();
        WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpoint.Id, @event);
        WebhookJobId jobId = handle.JobId;

        // Transport should have accepted the initial delivery job
        Assert.Single(transport.EnqueuedJobs);

        // ── STEP 2: WORKER PROCESSES ATTEMPT #1 (503 Error -> Schedules Retry)
        WebhookDeliveryJob jobAttempt1 = transport.EnqueuedJobs[0].Job;
        await jobHandler.HandleAsync(jobAttempt1);

        WebhookJobRecord? recordAfterAttempt1 = await store.GetJobAsync(jobId);
        Assert.NotNull(recordAfterAttempt1);
        Assert.Equal(WebhookJobStatus.Retrying, recordAfterAttempt1.Status);
        Assert.Single(recordAfterAttempt1.Attempts);

        // ── STEP 3: WORKER PROCESSES ATTEMPT #2 (Max Attempts Reached -> DeadLetter)
        Assert.Equal(2, transport.EnqueuedJobs.Count); // Retry middleware re-enqueued the job with original JobId
        WebhookDeliveryJob jobAttempt2 = transport.EnqueuedJobs[1].Job;
        await jobHandler.HandleAsync(jobAttempt2);

        // Assert: Job status must transition to DeadLettered
        WebhookJobRecord? recordDeadLettered = await store.GetJobAsync(jobId);
        Assert.NotNull(recordDeadLettered);
        Assert.Equal(WebhookJobStatus.DeadLettered, recordDeadLettered.Status);
        Assert.Equal(2, recordDeadLettered.Attempts.Count);

        // Assert: Dead-letter query returns the failed job
        IReadOnlyList<WebhookJobRecord> deadLetterList = await store.GetDeadLetteredJobsAsync(10);
        Assert.Single(deadLetterList);
        Assert.Equal(jobId, deadLetterList[0].Id);

        // ── STEP 4: OPERATOR / ADMIN TRIGGERS MANUAL REPLAY ──────────────────
        await dispatcher.ReplayAsync(jobId);

        WebhookJobRecord? recordReplayed = await store.GetJobAsync(jobId);
        Assert.NotNull(recordReplayed);
        Assert.Equal(WebhookJobStatus.Queued, recordReplayed.Status);

        // ── STEP 5: WORKER PROCESSES REPLAYED JOB (Success 200 OK) ────────────
        WebhookDeliveryJob jobReplay = transport.EnqueuedJobs[^1].Job;
        await jobHandler.HandleAsync(jobReplay);

        // Assert: Final state is Delivered with all 3 attempts preserved in audit history
        WebhookJobRecord? finalRecord = await store.GetJobAsync(jobId);
        Assert.NotNull(finalRecord);
        Assert.Equal(WebhookJobStatus.Delivered, finalRecord.Status);
        Assert.Equal(3, finalRecord.Attempts.Count);
        Assert.True(finalRecord.Attempts[2].IsSuccess);
    }
}