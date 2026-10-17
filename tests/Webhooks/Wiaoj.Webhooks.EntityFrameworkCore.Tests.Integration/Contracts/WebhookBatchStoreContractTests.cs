using Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Contracts;

/// <summary>
/// The behaviour every <see cref="IWebhookBatchStore"/> must have, written once and run against each backend.
/// </summary>
/// <remarks>
/// The in-memory batch store reads the system clock rather than a <see cref="TimeProvider"/>, so these tests never
/// move time: expiry is exercised with zero-length leases and thresholds derived from the stored lease.
/// </remarks>
public abstract class WebhookBatchStoreContractTests : StoreTestBase {
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private async Task<WebhookPublishBatchRecord> SavedBatchAsync(params string[] targets) {
        WebhookPublishBatchRecord batch = new(
            WebhookBatchId.NewId(),
            new WebhookNamespace("tenant-a"),
            "order.created",
            """{"orderId":7}""",
            targets.Length == 0 ? [new WebhookEndpointId("ep-1")] : [.. targets.Select(t => new WebhookEndpointId(t))],
            this.Time.GetUtcNow());

        await this.BatchStore.SaveBatchAsync(batch, TestContext.Current.CancellationToken);
        return batch;
    }

    private async Task<WebhookPublishBatchRecord> ReloadAsync(WebhookBatchId id) {
        WebhookPublishBatchRecord? batch = await this.BatchStore.GetBatchAsync(id, TestContext.Current.CancellationToken);
        Assert.NotNull(batch);
        return batch;
    }

    [Fact]
    public async Task SaveBatchAsync_ThenGetBatchAsync_ReturnsItWithTargetsInOrder() {
        WebhookPublishBatchRecord batch = await SavedBatchAsync("ep-c", "ep-a", "ep-b");

        WebhookPublishBatchRecord loaded = await ReloadAsync(batch.Id);

        Assert.Equal(batch.Namespace, loaded.Namespace);
        Assert.Equal(batch.EventName, loaded.EventName);
        Assert.Equal(batch.SerializedPayload, loaded.SerializedPayload);
        Assert.Equal(["ep-c", "ep-a", "ep-b"], loaded.TargetEndpoints.Select(e => e.Value));
        Assert.Equal(3, loaded.TargetSubscriberCount);
        Assert.Equal(0, loaded.DispatchedCount);
        Assert.Equal(WebhookBatchStatus.InFlight, loaded.Status);
        Assert.Equal(batch.CreatedAt, loaded.CreatedAt);
        Assert.Null(loaded.LockedBy);
        Assert.Null(loaded.LockExpiresAt);
    }

    [Fact]
    public async Task GetBatchAsync_Unknown_ReturnsNull() {
        Assert.Null(await this.BatchStore.GetBatchAsync(WebhookBatchId.NewId(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateBatchProgressAsync_SetsCountAndStatus() {
        WebhookPublishBatchRecord batch = await SavedBatchAsync("ep-1", "ep-2");

        await this.BatchStore.UpdateBatchProgressAsync(batch.Id, 2, WebhookBatchStatus.Completed, TestContext.Current.CancellationToken);

        WebhookPublishBatchRecord loaded = await ReloadAsync(batch.Id);
        Assert.Equal(2, loaded.DispatchedCount);
        Assert.Equal(WebhookBatchStatus.Completed, loaded.Status);
        Assert.True(loaded.UpdatedAt >= loaded.CreatedAt);
    }

    [Fact]
    public async Task TryClaimBatchLeaseAsync_UnknownBatch_ReturnsFalse() {
        Assert.False(await this.BatchStore.TryClaimBatchLeaseAsync(WebhookBatchId.NewId(), "worker-a", LeaseDuration, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryClaimBatchLeaseAsync_HeldByAnotherInstance_ReturnsFalse_AndTheHolderCanRenew() {
        WebhookPublishBatchRecord batch = await SavedBatchAsync();

        Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));
        Assert.False(await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, "worker-b", LeaseDuration, TestContext.Current.CancellationToken));
        Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        WebhookPublishBatchRecord loaded = await ReloadAsync(batch.Id);
        Assert.Equal("worker-a", loaded.LockedBy);
        Assert.Equal(WebhookBatchStatus.InFlight, loaded.Status);
        Assert.NotNull(loaded.LockExpiresAt);
    }

    [Fact]
    public async Task TryClaimBatchLeaseAsync_ExpiredLease_CanBeTakenByAnotherInstance() {
        WebhookPublishBatchRecord batch = await SavedBatchAsync();

        Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, "worker-a", TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, "worker-b", LeaseDuration, TestContext.Current.CancellationToken));

        Assert.Equal("worker-b", (await ReloadAsync(batch.Id)).LockedBy);
    }

    [Fact]
    public async Task TryClaimBatchLeaseAsync_ConcurrentClaimants_ExactlyOneWins() {
        WebhookPublishBatchRecord batch = await SavedBatchAsync();
        const int claimants = 16;

        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] claims = [.. Enumerable.Range(0, claimants).Select(async i => {
            await start.Task;
            return await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, $"worker-{i}", LeaseDuration, TestContext.Current.CancellationToken);
        })];

        start.SetResult();
        bool[] results = await Task.WhenAll(claims);

        Assert.Equal(1, results.Count(won => won));
        Assert.Equal($"worker-{Array.IndexOf(results, true)}", (await ReloadAsync(batch.Id)).LockedBy);
    }

    [Fact]
    public async Task GetStaleInFlightBatchesAsync_ReturnsInFlightBatchesWhoseLeaseExpiredBeforeTheThreshold() {
        WebhookPublishBatchRecord leased = await SavedBatchAsync();
        WebhookPublishBatchRecord neverLeased = await SavedBatchAsync();
        WebhookPublishBatchRecord completed = await SavedBatchAsync();
        Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(leased.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));
        Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(completed.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));
        await this.BatchStore.UpdateBatchProgressAsync(completed.Id, 1, WebhookBatchStatus.Completed, TestContext.Current.CancellationToken);

        DateTimeOffset expiry = (await ReloadAsync(leased.Id)).LockExpiresAt!.Value;

        Assert.Empty(await this.BatchStore.GetStaleInFlightBatchesAsync(expiry, 10, TestContext.Current.CancellationToken));

        IReadOnlyList<WebhookPublishBatchRecord> stale = await this.BatchStore.GetStaleInFlightBatchesAsync(expiry.AddMinutes(1), 10, TestContext.Current.CancellationToken);
        Assert.Equal([leased.Id], stale.Select(b => b.Id));
        Assert.DoesNotContain(neverLeased.Id, stale.Select(b => b.Id));
    }

    [Fact]
    public async Task GetStaleInFlightBatchesAsync_RespectsMaxCount() {
        for(int i = 0; i < 4; i++) {
            WebhookPublishBatchRecord batch = await SavedBatchAsync();
            Assert.True(await this.BatchStore.TryClaimBatchLeaseAsync(batch.Id, "worker-a", TimeSpan.Zero, TestContext.Current.CancellationToken));
        }

        DateTimeOffset later = DateTimeOffset.UtcNow.AddYears(5);
        Assert.Equal(2, (await this.BatchStore.GetStaleInFlightBatchesAsync(later, 2, TestContext.Current.CancellationToken)).Count);
    }
}

public sealed class InMemoryWebhookBatchStoreContractTests : WebhookBatchStoreContractTests {
    protected override StoreBackend CreateBackend() => new InMemoryStoreBackend();
}

public sealed class SqliteWebhookBatchStoreContractTests : WebhookBatchStoreContractTests {
    protected override StoreBackend CreateBackend() => new SqliteStoreBackend();
}

public sealed class PostgreSqlWebhookBatchStoreContractTests : WebhookBatchStoreContractTests {
    protected override StoreBackend CreateBackend() => new PostgreSqlStoreBackend();
}
