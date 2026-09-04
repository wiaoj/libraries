using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Recovery;

[Trait("Category", "Unit")]
[Trait("Feature", "Recovery")]
[Trait("Component", "BackgroundService")]
public sealed class StaleJobRecoveryServiceTests {

    [Fact]
    public async Task SweepAndRecoverAsync_ReEnqueuesStaleInFlightJobs_AndTransitionsStatusToQueued() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookRecoveryOptions options = new() {
            PollingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1)
        };

        // Stale Job: InFlight and lease expired 5 minutes ago -> Must be recovered
        WebhookJobId staleJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord staleRecord = new(
            staleJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-1\",\"Amount\":42.50}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.InFlight,
            LockedBy = "crashed-pod-1",
            LockExpiresAt = now.AddMinutes(-5)
        };
        await store.SaveAsync(staleRecord, TestContext.Current.CancellationToken);

        // Active Job: InFlight but lease is active for 5 more minutes -> Must remain untouched
        WebhookJobId activeJobId = WebhookJobId.NewJobId();
        WebhookJobRecord activeRecord = new(
            activeJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-2\",\"Amount\":99.00}",
            now.AddMinutes(-2)) {
            Status = WebhookJobStatus.InFlight,
            LockedBy = "healthy-pod-2",
            LockExpiresAt = now.AddMinutes(5)
        };
        await store.SaveAsync(activeRecord, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            recoveryOptions: options);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Only the stale job was recovered and re-queued
        Assert.Equal(1, recoveredCount);
        (WebhookDeliveryJob job, TimeSpan? _) = Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(staleJobId, job.Id);

        WebhookJobRecord? updatedStaleJob = await store.GetJobAsync(staleJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedStaleJob);
        Assert.Equal(WebhookJobStatus.Queued, updatedStaleJob.Status);

        WebhookJobRecord? untouchedActiveJob = await store.GetJobAsync(activeJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(untouchedActiveJob);
        Assert.Equal(WebhookJobStatus.InFlight, untouchedActiveJob.Status);
    }

    [Fact]
    public async Task SweepAndRecoverAsync_ReturnsZero_WhenNoStaleJobsExist() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recoveredCount);
        Assert.Empty(transport.EnqueuedJobs);
    }

    [Fact]
    public void Options_Validate_Throws_OnInvalidConfigurations() {
        WebhookRecoveryOptions options = new() {
            PollingInterval = TimeSpan.Zero
        };
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());

        options.PollingInterval = TimeSpan.FromSeconds(10);
        options.RecoveryLeaseDuration = TimeSpan.FromSeconds(-1);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());

        options.RecoveryLeaseDuration = TimeSpan.FromMinutes(1);
        options.QueuedJobStaleThreshold = TimeSpan.FromSeconds(-1);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());

        options.QueuedJobStaleThreshold = TimeSpan.FromMinutes(2);
        options.BatchSize = 0;
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public async Task SweepAndRecoverAsync_WhenZombieQueuedJobExceedsThreshold_RecoversAndEnqueuesJob() {
        // Arrange: Simulate dual-write crash where job stayed in Queued status for 10 minutes
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookOptions webhookOptions = new() {
            InstanceId = "k8s-worker-pod-alpha"
        };

        WebhookRecoveryOptions recoveryOptions = new() {
            PollingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1),
            QueuedJobStaleThreshold = TimeSpan.FromMinutes(2)
        };

        // 1. Zombie Queued Job: Created 10 minutes ago and never processed
        WebhookJobId zombieJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord zombieRecord = new(
            zombieJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-ZOMBIE\",\"Amount\":100.00}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.Queued
        };
        await store.SaveAsync(zombieRecord, TestContext.Current.CancellationToken);

        // 2. Fresh Queued Job: Created 30 seconds ago (must NOT be treated as zombie)
        WebhookJobId freshJobId = WebhookJobId.NewJobId();
        WebhookJobRecord freshRecord = new(
            freshJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-FRESH\",\"Amount\":50.00}",
            now.AddSeconds(-30)) {
            Status = WebhookJobStatus.Queued
        };
        await store.SaveAsync(freshRecord, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            webhookOptions: webhookOptions,
            recoveryOptions: recoveryOptions);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Exactly 1 zombie job recovered with custom InstanceId claimed
        Assert.Equal(1, recoveredCount);
        (WebhookDeliveryJob job, TimeSpan? _) = Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(zombieJobId, job.Id);

        WebhookJobRecord? updatedZombie = await store.GetJobAsync(zombieJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedZombie);
        Assert.Equal(WebhookJobStatus.Queued, updatedZombie.Status);
        Assert.Equal("k8s-worker-pod-alpha", updatedZombie.LockedBy);

        // Fresh job must remain untouched in the store
        WebhookJobRecord? untouchedFresh = await store.GetJobAsync(freshJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(untouchedFresh);
        Assert.Null(untouchedFresh.LockedBy);
    }

    [Fact]
    public async Task SweepAndRecoverAsync_UsesCustomConfiguredInstanceId_WhenClaimingLease() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        const string customPodName = "k8s-pod-custom-node-7";
        WebhookOptions webhookOptions = new() {
            InstanceId = customPodName
        };
        WebhookRecoveryOptions recoveryOptions = new() {
            RecoveryLeaseDuration = TimeSpan.FromMinutes(3)
        };

        WebhookJobId staleJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord staleRecord = new(
            staleJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-1\",\"Amount\":42.50}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.InFlight,
            LockedBy = "dead-node",
            LockExpiresAt = now.AddMinutes(-5)
        };
        await store.SaveAsync(staleRecord, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            webhookOptions: webhookOptions,
            recoveryOptions: recoveryOptions);

        // Act
        await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Transport dequeued and lease lock was claimed by custom instance ID
        Assert.Single(transport.EnqueuedJobs);

        WebhookJobRecord? updated = await store.GetJobAsync(staleJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(customPodName, updated.LockedBy);
    }

    [Fact]
    public async Task Should_RecoverOrphanedRetryingJob_When_NextAttemptAtHasPassed() {
        // Arrange: Retrying job whose NextAttemptAt is 5 minutes in the past -> Must be recovered
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookRecoveryOptions options = new() {
            PollingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1)
        };

        WebhookJobId retryingJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord retryingRecord = new(
            retryingJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-RETRY\",\"Amount\":42.50}",
            now.AddMinutes(-15)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(-5)
        };
        await store.SaveAsync(retryingRecord, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            recoveryOptions: options);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Orphaned retrying job was recovered and re-enqueued
        Assert.Equal(1, recoveredCount);
        (WebhookDeliveryJob job, TimeSpan? _) = Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(retryingJobId, job.Id);

        WebhookJobRecord? updatedJob = await store.GetJobAsync(retryingJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedJob);
        Assert.Equal(WebhookJobStatus.Queued, updatedJob.Status);
        Assert.Null(updatedJob.NextAttemptAt);
    }

    [Fact]
    public async Task Should_NotRecoverRetryingJob_When_NextAttemptAtIsInFuture() {
        // Arrange: Retrying job whose NextAttemptAt is 10 minutes in the future -> Must NOT be recovered
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookRecoveryOptions options = new() {
            PollingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1)
        };

        WebhookJobId futureRetryJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord futureRetryRecord = new(
            futureRetryJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-FUTURE\",\"Amount\":99.00}",
            now.AddMinutes(-5)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(10)
        };
        await store.SaveAsync(futureRetryRecord, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            recoveryOptions: options);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Future retrying job was NOT recovered
        Assert.Equal(0, recoveredCount);
        Assert.Empty(transport.EnqueuedJobs);

        WebhookJobRecord? untouchedJob = await store.GetJobAsync(futureRetryJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(untouchedJob);
        Assert.Equal(WebhookJobStatus.Retrying, untouchedJob.Status);
    }

    [Fact]
    public async Task Should_RecoverRetryingJob_When_TimeAdvancesPastNextAttemptAt() {
        // Arrange: Retrying job whose NextAttemptAt is 5 minutes from now
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookRecoveryOptions options = new() {
            PollingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1)
        };

        WebhookJobId retryJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord retryRecord = new(
            retryJobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-TIME\",\"Amount\":75.00}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(5)
        };
        await store.SaveAsync(retryRecord, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            recoveryOptions: options);

        // Act - First sweep: Job is not yet due
        int firstSweepCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: No recovery yet
        Assert.Equal(0, firstSweepCount);
        Assert.Empty(transport.EnqueuedJobs);

        // Act - Advance time past NextAttemptAt and sweep again
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        int secondSweepCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Now the job should be recovered
        Assert.Equal(1, secondSweepCount);
        (WebhookDeliveryJob job, TimeSpan? _) = Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(retryJobId, job.Id);

        WebhookJobRecord? updatedJob = await store.GetJobAsync(retryJobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedJob);
        Assert.Equal(WebhookJobStatus.Queued, updatedJob.Status);
        Assert.Null(updatedJob.NextAttemptAt);
    }

    #region Edge Case Tests (Issue #41)

    // ────────────────────────────────────────────────────────────────────────
    // Edge Case 1: NextAttemptAt == null (Defensive recovery for unset timestamp)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RecoverRetryingJobImmediately_When_NextAttemptAtIsNull() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-null-next");
        WebhookJobRecord record = new(
            jobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-NULL\",\"Amount\":10.00}",
            now.AddMinutes(-5)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = null // Null timestamp
        };
        await store.SaveAsync(record, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Treated as immediately due, recovered, and re-enqueued
        Assert.Equal(1, recoveredCount);
        (WebhookDeliveryJob job, TimeSpan? _) = Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(jobId, job.Id);

        WebhookJobRecord? updated = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.Queued, updated.Status);
        Assert.Null(updated.NextAttemptAt);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edge Case 2: Lease Lock State on Retrying Transitions
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RecoverRetryingJob_When_PriorExecutionLockWasReleasedOnRetryTransition() {
        // Arrange: Worker claims lease (InFlight), then delivery fails and transitions to Retrying
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-lock-test");
        WebhookJobRecord record = new(
            jobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-LOCK\",\"Amount\":25.00}",
            now.AddMinutes(-10));
        await store.SaveAsync(record, TestContext.Current.CancellationToken);

        // Worker claimed 5-minute lease
        bool claimed = await store.TryClaimLeaseAsync(jobId, "crashed-worker-pod", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        Assert.True(claimed);

        // Delivery failed -> Transitions to Retrying with NextAttemptAt in 15 seconds
        DateTimeOffset retryAt = now.AddSeconds(15);
        await store.UpdateStatusAsync(jobId, WebhookJobStatus.Retrying, retryAt, TestContext.Current.CancellationToken);

        // Verify that updating status to Retrying released the execution lock
        WebhookJobRecord? inStore = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(inStore);
        Assert.Null(inStore.LockedBy);
        Assert.Null(inStore.LockExpiresAt);

        // Advance time past retryAt
        timeProvider.Advance(TimeSpan.FromSeconds(20));

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        // Act: Recovery sweeps
        int recovered = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Job is recovered immediately without being blocked by previous 5-minute lease
        Assert.Equal(1, recovered);
        Assert.Single(transport.EnqueuedJobs);
    }

    [Fact]
    public async Task Should_NotRecoverRetryingJob_When_ActiveLeaseHeldByAnotherInstance() {
        // Arrange: Retrying job where another instance is currently claiming lease
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-active-lease");
        WebhookJobRecord record = new(
            jobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-ACTIVE\",\"Amount\":55.00}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(-2),
            LockedBy = "other-active-recovery-node",
            LockExpiresAt = now.AddMinutes(2) // Active lease for 2 more minutes
        };
        await store.SaveAsync(record, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        // Act
        int recovered = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Skipped because lease is held by another active node
        Assert.Equal(0, recovered);
        Assert.Empty(transport.EnqueuedJobs);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edge Case 3: Concurrent Multi-Node Race on Same Retrying Job
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AllowOnlySingleNodeToRecover_When_MultipleNodesRaceConcurrently() {
        // Arrange: 1 orphaned retrying job, 3 competing recovery instances
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-race");
        WebhookJobRecord record = new(
            jobId,
            endpointId,
            "order.created",
            "{\"OrderId\":\"ORD-RACE\",\"Amount\":99.99}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(-2)
        };
        await store.SaveAsync(record, TestContext.Current.CancellationToken);

        StaleJobRecoveryService node1 = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            webhookOptions: new WebhookOptions { InstanceId = "node-alpha" });

        StaleJobRecoveryService node2 = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            webhookOptions: new WebhookOptions { InstanceId = "node-beta" });

        StaleJobRecoveryService node3 = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            webhookOptions: new WebhookOptions { InstanceId = "node-gamma" });

        // Act: Run sweeps concurrently
        Task<int> task1 = node1.SweepAndRecoverAsync(TestContext.Current.CancellationToken);
        Task<int> task2 = node2.SweepAndRecoverAsync(TestContext.Current.CancellationToken);
        Task<int> task3 = node3.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        int[] results = await Task.WhenAll(task1, task2, task3);

        // Assert: Exactly 1 node won the lease race and recovered the job
        int totalRecovered = results.Sum();
        Assert.Equal(1, totalRecovered);
        Assert.Single(transport.EnqueuedJobs);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edge Case 4: Unregistered Event Type or Corrupt Payload During Recovery
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_TransitionToDeadLettered_When_EventTypeIsUnregistered() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-unknown-event");
        WebhookJobRecord record = new(
            jobId,
            endpointId,
            "deleted.event.schema.v99", // Unregistered event type
            "{\"OldData\":\"value\"}",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(-1)
        };
        await store.SaveAsync(record, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        // Act
        int recovered = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Job was dead-lettered, not re-enqueued, and recovery completed without throwing
        Assert.Equal(0, recovered);
        Assert.Empty(transport.EnqueuedJobs);

        WebhookJobRecord? updated = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.DeadLettered, updated.Status);
    }

    [Fact]
    public async Task Should_TransitionToDeadLettered_When_PayloadIsCorrupt() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-corrupt-payload");
        WebhookJobRecord record = new(
            jobId,
            endpointId,
            "order.created",
            "NOT_VALID_JSON_CORRUPTED_STREAM{{{",
            now.AddMinutes(-10)) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(-1)
        };
        await store.SaveAsync(record, TestContext.Current.CancellationToken);

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider);

        // Act
        int recovered = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);

        // Assert: Exception caught, dead-lettered, loop did not crash
        Assert.Equal(0, recovered);
        Assert.Empty(transport.EnqueuedJobs);

        WebhookJobRecord? updated = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.DeadLettered, updated.Status);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edge Case 5: Batch Size Limits & Pagination
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RespectBatchSize_And_RecoverRemainingJobsInSubsequentSweep() {
        // Arrange: 25 orphaned retrying jobs, BatchSize = 10
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore store = new(timeProvider);
        FakeWebhookTransport transport = new();

        WebhookRecoveryOptions options = new() {
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1)
        };

        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-batch-paging");

        for(int i = 1; i <= 25; i++) {
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookJobRecord record = new(
                jobId,
                endpointId,
                "order.created",
                $"{{\"OrderId\":\"ORD-BATCH-{i}\",\"Amount\":{i}.00}}",
                now.AddMinutes(-15)) {
                Status = WebhookJobStatus.Retrying,
                NextAttemptAt = now.AddMinutes(-5)
            };
            await store.SaveAsync(record, TestContext.Current.CancellationToken);
        }

        StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(
            store: store,
            transport: transport,
            timeProvider: timeProvider,
            recoveryOptions: options);

        // Act & Assert - Sweep 1: recovers exactly 10
        int sweep1 = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, sweep1);
        Assert.Equal(10, transport.EnqueuedJobs.Count);

        // Sweep 2: recovers next 10
        int sweep2 = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, sweep2);
        Assert.Equal(20, transport.EnqueuedJobs.Count);

        // Sweep 3: recovers remaining 5
        int sweep3 = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, sweep3);
        Assert.Equal(25, transport.EnqueuedJobs.Count);

        // Sweep 4: no jobs left to recover
        int sweep4 = await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, sweep4);
        Assert.Equal(25, transport.EnqueuedJobs.Count);
    }

    #endregion
}