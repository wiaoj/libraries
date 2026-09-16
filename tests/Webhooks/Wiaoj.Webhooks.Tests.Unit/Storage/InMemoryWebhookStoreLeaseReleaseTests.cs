using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Storage;

/// <summary>
/// A finished job can't be leased, and leaving the in-flight states releases the lease (#41).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "InMemoryWebhookStore")]
public sealed class InMemoryWebhookStoreLeaseReleaseTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(InMemoryWebhookStore Store, WebhookJobId JobId)> SeedLeasedAsync() {
        InMemoryWebhookStore store = new(new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        WebhookJobId jobId = WebhookJobId.NewJobId();
        await store.SaveAsync(new WebhookJobRecord(jobId, WebhookTestFactory.CreateEndpointId(), "order.paid", "{}", DateTimeOffset.UtcNow), Ct);
        Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(10), Ct));
        return (store, jobId);
    }

    [Theory]
    [InlineData(WebhookJobStatus.Delivered)]
    [InlineData(WebhookJobStatus.DeadLettered)]
    public async Task TryClaimLeaseAsync_Refuses_AFinishedJob_EvenForTheSameInstance(WebhookJobStatus finished) {
        (InMemoryWebhookStore store, WebhookJobId jobId) = await SeedLeasedAsync();
        await store.UpdateStatusAsync(jobId, finished, Ct);

        Assert.False(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(10), Ct));
        Assert.False(await store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(10), Ct));
        WebhookJobRecord record = (await store.GetJobAsync(jobId, Ct))!;
        Assert.Equal(finished, record.Status);
        Assert.Null(record.LockedBy);
    }

    [Theory]
    [InlineData(WebhookJobStatus.Retrying, true)]
    [InlineData(WebhookJobStatus.Delivered, true)]
    [InlineData(WebhookJobStatus.DeadLettered, true)]
    [InlineData(WebhookJobStatus.Queued, false)]
    [InlineData(WebhookJobStatus.InFlight, false)]
    public async Task UpdateStatusAsync_ReleasesTheLease_OnlyWhenLeavingTheInFlightStates(WebhookJobStatus status, bool released) {
        (InMemoryWebhookStore first, WebhookJobId firstId) = await SeedLeasedAsync();
        (InMemoryWebhookStore second, WebhookJobId secondId) = await SeedLeasedAsync();

        await first.UpdateStatusAsync(firstId, status, Ct);
        await second.UpdateStatusAsync(secondId, status, nextAttemptAt: null, Ct);

        foreach(WebhookJobRecord record in new[] { (await first.GetJobAsync(firstId, Ct))!, (await second.GetJobAsync(secondId, Ct))! }) {
            Assert.Equal(status, record.Status);
            Assert.Equal(released ? null : "pod-1", record.LockedBy);
            Assert.Equal(released, record.LockExpiresAt is null);
        }
    }

    [Fact]
    public async Task AReplayedJob_CanBeLeasedByAnotherInstance_RightAfterItWasDelivered() {
        (InMemoryWebhookStore store, WebhookJobId jobId) = await SeedLeasedAsync();
        await store.UpdateStatusAsync(jobId, WebhookJobStatus.Delivered, Ct);

        await store.UpdateStatusAsync(jobId, WebhookJobStatus.Queued, Ct); // what WebhookDispatcher.ReplayAsync does

        Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(10), Ct));
    }
}
