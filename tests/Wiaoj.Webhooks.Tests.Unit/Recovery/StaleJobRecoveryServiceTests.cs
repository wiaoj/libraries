using Microsoft.Extensions.Logging.Abstractions;
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
        InMemoryWebhookStore store = new();
        FakeWebhookTransport transport = new();
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        timeProvider.SetUtcNow(now);

        WebhookRecoveryOptions options = new() {
            PollingInterval = TimeSpan.FromSeconds(10),
            BatchSize = 10,
            RecoveryLeaseDuration = TimeSpan.FromMinutes(1)
        };

        // Stale Job 1: InFlight and lock expired 5 minutes ago -> Must be recovered
        WebhookJobId staleJobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord staleRecord = new(staleJobId, endpointId, "order.created", """{"id":1}""", now.AddMinutes(-10)) {
            Status = WebhookJobStatus.InFlight,
            LockedBy = "crashed-pod-1",
            LockExpiresAt = now.AddMinutes(-5)
        };
        await store.SaveAsync(staleRecord);

        // Active Job 2: InFlight but lease is still active for 5 more minutes -> Must NOT be touched
        WebhookJobId activeJobId = WebhookJobId.NewJobId();
        WebhookJobRecord activeRecord = new(activeJobId, endpointId, "order.created", """{"id":2}""", now.AddMinutes(-2)) {
            Status = WebhookJobStatus.InFlight,
            LockedBy = "healthy-pod-2",
            LockExpiresAt = now.AddMinutes(5)
        };
        await store.SaveAsync(activeRecord);

        StaleJobRecoveryService service = new(
            store,
            transport,
            timeProvider,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<StaleJobRecoveryService>.Instance);

        // Act
        int recoveredCount = await service.SweepAndRecoverAsync();

        // Assert
        Assert.Equal(1, recoveredCount);

        // Stale job must be in transport and its status must be Queued
        Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(staleJobId, transport.EnqueuedJobs[0].Job.Id);

        WebhookJobRecord? updatedStaleJob = await store.GetJobAsync(staleJobId);
        Assert.NotNull(updatedStaleJob);
        Assert.Equal(WebhookJobStatus.Queued, updatedStaleJob.Status);

        // Active job must remain untouched in InFlight status
        WebhookJobRecord? untouchedActiveJob = await store.GetJobAsync(activeJobId);
        Assert.NotNull(untouchedActiveJob);
        Assert.Equal(WebhookJobStatus.InFlight, untouchedActiveJob.Status);
    }

    [Fact]
    public async Task SweepAndRecoverAsync_ReturnsZero_WhenNoStaleJobsExist() {
        InMemoryWebhookStore store = new();
        FakeWebhookTransport transport = new();
        FakeTimeProvider timeProvider = new();

        StaleJobRecoveryService service = new(
            store,
            transport,
            timeProvider,
            Microsoft.Extensions.Options.Options.Create(new WebhookRecoveryOptions()),
            NullLogger<StaleJobRecoveryService>.Instance);

        int recoveredCount = await service.SweepAndRecoverAsync();

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
        options.BatchSize = 0;
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.Validate());
    }
}