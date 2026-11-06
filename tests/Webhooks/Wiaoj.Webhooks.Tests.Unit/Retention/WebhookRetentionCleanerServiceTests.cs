using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Retention;

[Trait("Category", "Unit")]
[Trait("Feature", "Retention")]
[Trait("Component", "BackgroundService")]
public sealed class WebhookRetentionCleanerServiceTests {
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly InMemoryWebhookStore _store;

    public WebhookRetentionCleanerServiceTests() {
        this._store = new InMemoryWebhookStore(this._timeProvider);
    }

    private async Task<WebhookJobId> SaveFinishedJobAsync(WebhookJobStatus status, DateTimeOffset completedAt) {
        WebhookJobRecord record = new(
            WebhookJobId.NewJobId(),
            WebhookTestFactory.CreateEndpointId("retention"),
            "order.created",
            "{}",
            completedAt.AddMinutes(-5)) {
            Status = status,
            CompletedAt = completedAt
        };
        await this._store.SaveAsync(record, TestContext.Current.CancellationToken);
        return record.Id;
    }

    [Fact]
    public async Task SweepAsync_DeletesOnlyJobsPastTheirRetentionWindow() {
        WebhookJobId expiredDelivered = await SaveFinishedJobAsync(WebhookJobStatus.Delivered, Now.AddDays(-8));
        WebhookJobId keptDelivered = await SaveFinishedJobAsync(WebhookJobStatus.Delivered, Now.AddDays(-6));
        WebhookJobId expiredDeadLettered = await SaveFinishedJobAsync(WebhookJobStatus.DeadLettered, Now.AddDays(-31));
        WebhookJobId keptDeadLettered = await SaveFinishedJobAsync(WebhookJobStatus.DeadLettered, Now.AddDays(-29));

        WebhookJobId activeJob = WebhookJobId.NewJobId();
        await this._store.SaveAsync(new WebhookJobRecord(
            activeJob,
            WebhookTestFactory.CreateEndpointId("retention"),
            "order.created",
            "{}",
            Now.AddDays(-90)) {
            Status = WebhookJobStatus.InFlight,
            LockedBy = "pod-1",
            LockExpiresAt = Now.AddMinutes(5)
        }, TestContext.Current.CancellationToken);

        WebhookRetentionCleanerService service = WebhookTestFactory.CreateRetentionCleanerService(
            store: this._store,
            timeProvider: this._timeProvider);

        int deleted = await service.SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, deleted);
        Assert.Null(await this._store.GetJobAsync(expiredDelivered, TestContext.Current.CancellationToken));
        Assert.Null(await this._store.GetJobAsync(expiredDeadLettered, TestContext.Current.CancellationToken));
        Assert.NotNull(await this._store.GetJobAsync(keptDelivered, TestContext.Current.CancellationToken));
        Assert.NotNull(await this._store.GetJobAsync(keptDeadLettered, TestContext.Current.CancellationToken));
        Assert.NotNull(await this._store.GetJobAsync(activeJob, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SweepAsync_DrainsTheBacklogInBatches() {
        for(int i = 0; i < 5; i++) {
            await SaveFinishedJobAsync(WebhookJobStatus.Delivered, Now.AddDays(-30));
        }

        WebhookRetentionCleanerService service = WebhookTestFactory.CreateRetentionCleanerService(
            store: this._store,
            timeProvider: this._timeProvider,
            retentionOptions: new WebhookRetentionOptions { BatchSize = 2 });

        int deleted = await service.SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, deleted);
        Assert.Empty(await this._store.GetHistoryByEndpointAsync(WebhookTestFactory.CreateEndpointId("retention"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SweepAsync_ReturnsZero_WhenNothingIsExpired() {
        await SaveFinishedJobAsync(WebhookJobStatus.Delivered, Now.AddMinutes(-1));

        WebhookRetentionCleanerService service = WebhookTestFactory.CreateRetentionCleanerService(
            store: this._store,
            timeProvider: this._timeProvider);

        int deleted = await service.SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task SweepAsync_UsesTheClockToComputeCutoffs() {
        WebhookJobId finished = await SaveFinishedJobAsync(WebhookJobStatus.Delivered, Now);

        WebhookRetentionCleanerService service = WebhookTestFactory.CreateRetentionCleanerService(
            store: this._store,
            timeProvider: this._timeProvider);
        Assert.Equal(0, await service.SweepAsync(TestContext.Current.CancellationToken));

        // Advancing the clock past the retention window makes the same job prunable.
        this._timeProvider.SetUtcNow(Now.AddDays(8));

        int deleted = await service.SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, deleted);
        Assert.Null(await this._store.GetJobAsync(finished, TestContext.Current.CancellationToken));
    }
}
