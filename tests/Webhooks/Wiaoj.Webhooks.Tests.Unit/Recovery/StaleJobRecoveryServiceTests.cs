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
    public async Task SweepAndRecoverAsync_RecoversOrphanedRetryingJobs_WhenNextAttemptAtHasPassed() {
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
    }

    [Fact]
    public async Task SweepAndRecoverAsync_DoesNotRecover_RetryingJobsWithFutureNextAttemptAt() {
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
    public async Task SweepAndRecoverAsync_RecoversRetryingJobs_AfterTimeAdvance() {
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
    }
}