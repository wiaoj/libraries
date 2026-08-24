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
}