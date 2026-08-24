using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Storage;

public sealed class InMemoryWebhookStoreTests {
    private readonly InMemoryWebhookStore _store = new();

    [Fact]
    public async Task SaveAsync_And_GetJobAsync_ReturnsSavedRecord() {
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
        WebhookJobRecord record = new(jobId, endpointId, "OrderCreated", """{"amount":99}""", DateTimeOffset.UtcNow);

        await this._store.SaveAsync(record, TestContext.Current.CancellationToken);

        WebhookJobRecord? retrieved = await this._store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(retrieved);
        Assert.Equal(jobId, retrieved.Id);
        Assert.Equal(endpointId, retrieved.EndpointId);
        Assert.Equal("OrderCreated", retrieved.EventType);
        Assert.Equal("""{"amount":99}""", retrieved.SerializedPayload);
        Assert.Equal(WebhookJobStatus.Queued, retrieved.Status);
    }

    [Fact]
    public async Task SaveAsync_ThrowsWhenJobIsNull() {
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => this._store.SaveAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetJobAsync_ReturnsNull_WhenJobNotFound() {
        WebhookJobRecord? retrieved = await this._store.GetJobAsync(WebhookJobId.NewJobId(), TestContext.Current.CancellationToken);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetHistoryByEndpointAsync_ReturnsAllJobsForEndpoint() {
        WebhookEndpointId targetEndpoint = WebhookTestFactory.CreateEndpointId("endpoint-history");
        WebhookEndpointId otherEndpoint = WebhookTestFactory.CreateEndpointId("endpoint-other");

        WebhookJobRecord job1 = new(WebhookJobId.NewJobId(), targetEndpoint, "E1", "{}", DateTimeOffset.UtcNow);
        WebhookJobRecord job2 = new(WebhookJobId.NewJobId(), otherEndpoint, "E2", "{}", DateTimeOffset.UtcNow);
        WebhookJobRecord job3 = new(WebhookJobId.NewJobId(), targetEndpoint, "E3", "{}", DateTimeOffset.UtcNow);

        await this._store.SaveAsync(job1, TestContext.Current.CancellationToken);
        await this._store.SaveAsync(job2, TestContext.Current.CancellationToken);
        await this._store.SaveAsync(job3, TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookJobRecord> history = await this._store.GetHistoryByEndpointAsync(targetEndpoint, TestContext.Current.CancellationToken);

        Assert.Equal(2, history.Count);
        Assert.Contains(history, j => j.Id == job1.Id);
        Assert.Contains(history, j => j.Id == job3.Id);
    }

    [Fact]
    public async Task GetHistoryByEndpointAsync_ReturnsEmpty_WhenEndpointHasNoJobs() {
        IReadOnlyList<WebhookJobRecord> history = await this._store.GetHistoryByEndpointAsync(WebhookTestFactory.CreateEndpointId("non-existent"), TestContext.Current.CancellationToken);
        Assert.Empty(history);
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatusCorrectly() {
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "E", "{}", DateTimeOffset.UtcNow);
        await this._store.SaveAsync(record, TestContext.Current.CancellationToken);

        await this._store.UpdateStatusAsync(jobId, WebhookJobStatus.InFlight, TestContext.Current.CancellationToken);
        WebhookJobRecord? updated = await this._store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.InFlight, updated.Status);

        await this._store.UpdateStatusAsync(jobId, WebhookJobStatus.Delivered, TestContext.Current.CancellationToken);
        updated = await this._store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(WebhookJobStatus.Delivered, updated.Status);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_AllowsOnlyOneInstanceToClaimActiveLease() {
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "E", "{}", DateTimeOffset.UtcNow);
        await this._store.SaveAsync(record, TestContext.Current.CancellationToken);

        // Instance 1 claims lease for 1 minute
        bool claimed1 = await this._store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.True(claimed1);

        // Instance 2 tries to claim while lease is active -> fails
        bool claimed2 = await this._store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.False(claimed2);

        // Instance 1 can renew its own lease
        bool renewed = await this._store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
        Assert.True(renewed);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_AllowsAnotherInstanceToClaim_WhenLeaseExpires() {
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "E", "{}", DateTimeOffset.UtcNow);
        await this._store.SaveAsync(record, TestContext.Current.CancellationToken);

        // Claimed with zero duration so it expires immediately
        await this._store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.Zero, TestContext.Current.CancellationToken);

        // Now pod-2 can claim it because lock expired
        bool claimedByPod2 = await this._store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.True(claimedByPod2);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_ThrowsWhenParametersInvalid() {
        WebhookJobId jobId = WebhookJobId.NewJobId();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => this._store.TryClaimLeaseAsync(jobId, "", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => this._store.TryClaimLeaseAsync(jobId, "pod", TimeSpan.FromMinutes(-1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryClaimLeaseAsync_ReturnsFalse_WhenJobNotFound() {
        bool claimed = await this._store.TryClaimLeaseAsync(WebhookJobId.NewJobId(), "pod-1", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.False(claimed);
    }

    [Fact]
    public async Task RecordAttemptAsync_AppendsAttemptsChronologically() {
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
        WebhookJobRecord record = new(jobId, endpointId, "E", "{}", DateTimeOffset.UtcNow);
        await this._store.SaveAsync(record, TestContext.Current.CancellationToken);

        WebhookDeliveryAttempt attempt1 = new(
            endpointId,
            attemptNumber: 1,
            attemptedAt: UnixTimestamp.Now,
            duration: TimeSpan.FromMilliseconds(50),
            result: WebhookDeliveryResult.Transient("503 Service Unavailable", 503));

        WebhookDeliveryAttempt attempt2 = new(
            endpointId,
            attemptNumber: 2,
            attemptedAt: UnixTimestamp.Now,
            duration: TimeSpan.FromMilliseconds(45),
            result: WebhookDeliveryResult.Success(200, "OK"));

        await this._store.RecordAttemptAsync(jobId, attempt1, TestContext.Current.CancellationToken);
        await this._store.RecordAttemptAsync(jobId, attempt2, TestContext.Current.CancellationToken);

        WebhookJobRecord? retrieved = await this._store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.Attempts.Count);
        Assert.False(retrieved.Attempts[0].IsSuccess);
        Assert.True(retrieved.Attempts[1].IsSuccess);
    }

    [Fact]
    public async Task GetStaleInFlightJobsAsync_ReturnsOnlyExpiredInFlightJobs() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WebhookJobRecord staleJob1 = new(WebhookJobId.NewJobId(), WebhookTestFactory.CreateEndpointId(), "E", "{}", now) {
            Status = WebhookJobStatus.InFlight,
            LockExpiresAt = now.AddMinutes(-5)
        };
        WebhookJobRecord staleJob2 = new(WebhookJobId.NewJobId(), WebhookTestFactory.CreateEndpointId(), "E", "{}", now) {
            Status = WebhookJobStatus.InFlight,
            LockExpiresAt = now.AddMinutes(-2)
        };
        WebhookJobRecord activeJob = new(WebhookJobId.NewJobId(), WebhookTestFactory.CreateEndpointId(), "E", "{}", now) {
            Status = WebhookJobStatus.InFlight,
            LockExpiresAt = now.AddMinutes(5)
        };
        WebhookJobRecord completedJob = new(WebhookJobId.NewJobId(), WebhookTestFactory.CreateEndpointId(), "E", "{}", now) {
            Status = WebhookJobStatus.Delivered,
            LockExpiresAt = now.AddMinutes(-5)
        };

        await this._store.SaveAsync(staleJob1, TestContext.Current.CancellationToken);
        await this._store.SaveAsync(staleJob2, TestContext.Current.CancellationToken);
        await this._store.SaveAsync(activeJob, TestContext.Current.CancellationToken);
        await this._store.SaveAsync(completedJob, TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookJobRecord> stale = await this._store.GetExpiredInFlightJobsAsync(now, maxCount: 10, TestContext.Current.CancellationToken);
        Assert.Equal(2, stale.Count);
        Assert.Contains(stale, j => j.Id == staleJob1.Id);
        Assert.Contains(stale, j => j.Id == staleJob2.Id);
    }

    [Fact]
    public async Task NullWebhookStore_ReturnsSafeDefaults() {
        NullWebhookStore nullStore = NullWebhookStore.Instance;
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "E", "{}", DateTimeOffset.UtcNow);

        await nullStore.SaveAsync(record, TestContext.Current.CancellationToken);
        Assert.Null(await nullStore.GetJobAsync(jobId, TestContext.Current.CancellationToken));
        Assert.Empty(await nullStore.GetHistoryByEndpointAsync(WebhookTestFactory.CreateEndpointId(), TestContext.Current.CancellationToken));
        await nullStore.UpdateStatusAsync(jobId, WebhookJobStatus.Delivered, TestContext.Current.CancellationToken);
        Assert.True(await nullStore.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        await nullStore.RecordAttemptAsync(jobId, WebhookTestFactory.CreateAttempt(), TestContext.Current.CancellationToken);
        Assert.Empty(await nullStore.GetExpiredInFlightJobsAsync(DateTimeOffset.UtcNow, 10, TestContext.Current.CancellationToken));
    }
}
