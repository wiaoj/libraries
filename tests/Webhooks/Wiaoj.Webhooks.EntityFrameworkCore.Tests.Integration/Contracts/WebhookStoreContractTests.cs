using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Primitives;
using Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Contracts;

/// <summary>
/// The behaviour every <see cref="IWebhookStore"/> must have, written once and run against each backend.
/// </summary>
public abstract class WebhookStoreContractTests : StoreTestBase {
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    protected WebhookJobRecord NewJob(string endpoint = "ep-1", TimeSpan? age = null, string? batchId = null) {
        return new WebhookJobRecord(
            WebhookJobId.NewJobId(),
            new WebhookEndpointId(endpoint),
            $"{endpoint}-partition",
            "order.created",
            """{"orderId":42}""",
            this.Time.GetUtcNow() - (age ?? TimeSpan.Zero)) {
            BatchId = batchId
        };
    }

    private async Task<WebhookJobRecord> SavedJobAsync(string endpoint = "ep-1", TimeSpan? age = null) {
        WebhookJobRecord job = NewJob(endpoint, age);
        await this.JobStore.SaveAsync(job, TestContext.Current.CancellationToken);
        return job;
    }

    private async Task<WebhookJobRecord> ReloadAsync(WebhookJobId id) {
        WebhookJobRecord? job = await this.JobStore.GetJobAsync(id, TestContext.Current.CancellationToken);
        Assert.NotNull(job);
        return job;
    }

    // ── Save / Get ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetJobAsync_ReturnsTheJob() {
        WebhookJobRecord job = NewJob(batchId: "batch_1");
        await this.JobStore.SaveAsync(job, TestContext.Current.CancellationToken);

        WebhookJobRecord loaded = await ReloadAsync(job.Id);

        Assert.Equal(job.Id, loaded.Id);
        Assert.Equal(job.EndpointId, loaded.EndpointId);
        Assert.Equal(job.PartitionKey, loaded.PartitionKey);
        Assert.Equal(job.EventType, loaded.EventType);
        Assert.Equal(job.SerializedPayload, loaded.SerializedPayload);
        Assert.Equal(job.CreatedAt, loaded.CreatedAt);
        Assert.Equal("batch_1", loaded.BatchId);
        Assert.Equal(WebhookJobStatus.Queued, loaded.Status);
        Assert.Null(loaded.NextAttemptAt);
        Assert.Null(loaded.LockedBy);
        Assert.Null(loaded.LockExpiresAt);
        Assert.Empty(loaded.Attempts);
    }

    [Fact]
    public async Task GetJobAsync_UnknownJob_ReturnsNull() {
        Assert.Null(await this.JobStore.GetJobAsync(WebhookJobId.NewJobId(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveBatchAsync_PersistsEveryJob() {
        WebhookJobRecord[] jobs = [NewJob(batchId: "batch_x"), NewJob(batchId: "batch_x"), NewJob("ep-2", batchId: "batch_x")];

        await this.JobStore.SaveBatchAsync(jobs, TestContext.Current.CancellationToken);

        foreach(WebhookJobRecord job in jobs) {
            WebhookJobRecord loaded = await ReloadAsync(job.Id);
            Assert.Equal(job.EndpointId, loaded.EndpointId);
            Assert.Equal("batch_x", loaded.BatchId);
        }
    }

    [Fact]
    public async Task SaveBatchAsync_EmptyList_DoesNothing() {
        await this.JobStore.SaveBatchAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(await this.JobStore.GetHistoryByEndpointAsync(new WebhookEndpointId("ep-1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHistoryByEndpointAsync_ReturnsOnlyThatEndpointsJobs() {
        WebhookJobRecord a1 = await SavedJobAsync("ep-a");
        WebhookJobRecord a2 = await SavedJobAsync("ep-a");
        await SavedJobAsync("ep-b");

        IReadOnlyList<WebhookJobRecord> history = await this.JobStore.GetHistoryByEndpointAsync(new WebhookEndpointId("ep-a"), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { a1.Id, a2.Id }.Order(), history.Select(j => j.Id).Order());
    }

    // ── Attempts ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordAttemptAsync_AppendsAttemptsInOrderWithTheirResults() {
        WebhookJobRecord job = await SavedJobAsync();
        UnixTimestamp at = UnixTimestamp.From(this.Time.GetUtcNow());
        WebhookDeliveryAttempt[] attempts = [
            WebhookDeliveryAttempt.Create(job.EndpointId, 1, at, TimeSpan.FromMilliseconds(120),
                new WebhookDeliveryResult.TransientFailure("503 from upstream", 503, TimeSpan.FromSeconds(30), TransientFailureReason.ServerUnavailable)),
            WebhookDeliveryAttempt.Create(job.EndpointId, 2, at.AddSeconds(30), TimeSpan.FromMilliseconds(80),
                new WebhookDeliveryResult.PermanentFailure("gone", 410, PermanentFailureReason.DestinationRejected)),
            WebhookDeliveryAttempt.CreateReplay(job.EndpointId, 3, at.AddSeconds(60), TimeSpan.FromMilliseconds(40),
                new WebhookDeliveryResult.Delivered(200, "ok")),
            WebhookDeliveryAttempt.Create(job.EndpointId, 4, at.AddSeconds(90), TimeSpan.Zero,
                new WebhookDeliveryResult.Deduplicated("dedup-key"))
        ];

        foreach(WebhookDeliveryAttempt attempt in attempts) {
            await this.JobStore.RecordAttemptAsync(job.Id, attempt, TestContext.Current.CancellationToken);
        }

        WebhookJobRecord loaded = await ReloadAsync(job.Id);
        Assert.Equal(attempts, loaded.Attempts);
    }

    [Fact]
    public async Task RecordAttemptAsync_UnknownJob_IsIgnored() {
        WebhookJobId unknown = WebhookJobId.NewJobId();

        await this.JobStore.RecordAttemptAsync(
            unknown,
            WebhookDeliveryAttempt.Create(new WebhookEndpointId("ep-1"), 1, UnixTimestamp.From(this.Time.GetUtcNow()), TimeSpan.Zero, WebhookDeliveryResult.Success()),
            TestContext.Current.CancellationToken);

        Assert.Null(await this.JobStore.GetJobAsync(unknown, TestContext.Current.CancellationToken));
    }

    // ── Leases ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryClaimLeaseAsync_UnknownJob_ReturnsFalse() {
        Assert.False(await this.JobStore.TryClaimLeaseAsync(WebhookJobId.NewJobId(), "worker-a", LeaseDuration, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryClaimLeaseAsync_FreeJob_TakesTheLeaseAndMovesToInFlight() {
        WebhookJobRecord job = await SavedJobAsync();

        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        WebhookJobRecord loaded = await ReloadAsync(job.Id);
        Assert.Equal(WebhookJobStatus.InFlight, loaded.Status);
        Assert.Equal("worker-a", loaded.LockedBy);
        Assert.Equal(this.Time.GetUtcNow() + LeaseDuration, loaded.LockExpiresAt);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_LeaseHeldByAnotherInstance_ReturnsFalseAndKeepsTheLease() {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        this.Time.Advance(LeaseDuration - TimeSpan.FromSeconds(1));
        Assert.False(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-b", LeaseDuration, TestContext.Current.CancellationToken));

        Assert.Equal("worker-a", (await ReloadAsync(job.Id)).LockedBy);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_SameInstance_RenewsItsLease() {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        this.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        Assert.Equal(this.Time.GetUtcNow() + LeaseDuration, (await ReloadAsync(job.Id)).LockExpiresAt);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_ExpiredLease_CanBeTakenByAnotherInstance() {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        this.Time.Advance(LeaseDuration);
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-b", LeaseDuration, TestContext.Current.CancellationToken));

        Assert.Equal("worker-b", (await ReloadAsync(job.Id)).LockedBy);
    }

    [Theory]
    [InlineData(WebhookJobStatus.Delivered)]
    [InlineData(WebhookJobStatus.DeadLettered)]
    public async Task TryClaimLeaseAsync_FinishedJob_ReturnsFalse(WebhookJobStatus finished) {
        WebhookJobRecord job = await SavedJobAsync();
        await this.JobStore.UpdateStatusAsync(job.Id, finished, TestContext.Current.CancellationToken);

        Assert.False(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));
        Assert.Equal(finished, (await ReloadAsync(job.Id)).Status);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_ConcurrentClaimants_ExactlyOneWins() {
        WebhookJobRecord job = await SavedJobAsync();
        const int claimants = 16;

        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] claims = [.. Enumerable.Range(0, claimants).Select(async i => {
            await start.Task;
            return await this.JobStore.TryClaimLeaseAsync(job.Id, $"worker-{i}", LeaseDuration, TestContext.Current.CancellationToken);
        })];

        start.SetResult();
        bool[] results = await Task.WhenAll(claims);

        Assert.Equal(1, results.Count(won => won));
        int winner = Array.IndexOf(results, true);
        Assert.Equal($"worker-{winner}", (await ReloadAsync(job.Id)).LockedBy);
    }

    [Fact]
    public async Task TryClaimLeaseAsync_InvalidArguments_Throw() {
        WebhookJobRecord job = await SavedJobAsync();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => this.JobStore.TryClaimLeaseAsync(job.Id, " ", LeaseDuration, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", TimeSpan.FromSeconds(-1), TestContext.Current.CancellationToken));
    }

    // ── Status transitions ───────────────────────────────────────────────────

    [Theory]
    [InlineData(WebhookJobStatus.Retrying)]
    [InlineData(WebhookJobStatus.Delivered)]
    [InlineData(WebhookJobStatus.DeadLettered)]
    public async Task UpdateStatusAsync_LeavingInFlight_ReleasesTheLease(WebhookJobStatus status) {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        await this.JobStore.UpdateStatusAsync(job.Id, status, TestContext.Current.CancellationToken);

        WebhookJobRecord loaded = await ReloadAsync(job.Id);
        Assert.Equal(status, loaded.Status);
        Assert.Null(loaded.LockedBy);
        Assert.Null(loaded.LockExpiresAt);
    }

    [Theory]
    [InlineData(WebhookJobStatus.Queued)]
    [InlineData(WebhookJobStatus.InFlight)]
    public async Task UpdateStatusAsync_ToQueuedOrInFlight_KeepsTheLease(WebhookJobStatus status) {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        await this.JobStore.UpdateStatusAsync(job.Id, status, TestContext.Current.CancellationToken);

        WebhookJobRecord loaded = await ReloadAsync(job.Id);
        Assert.Equal(status, loaded.Status);
        Assert.Equal("worker-a", loaded.LockedBy);
        Assert.Equal(this.Time.GetUtcNow() + LeaseDuration, loaded.LockExpiresAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_RetryingReleasesLease_SoAnotherInstanceCanClaimAtOnce() {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", LeaseDuration, TestContext.Current.CancellationToken));

        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Retrying, this.Time.GetUtcNow().AddSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-b", LeaseDuration, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateStatusAsync_WithNextAttempt_SetsAndClearsTheSchedule() {
        WebhookJobRecord job = await SavedJobAsync();
        DateTimeOffset next = this.Time.GetUtcNow().AddMinutes(2);

        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Retrying, next, TestContext.Current.CancellationToken);
        WebhookJobRecord retrying = await ReloadAsync(job.Id);
        Assert.Equal(WebhookJobStatus.Retrying, retrying.Status);
        Assert.Equal(next, retrying.NextAttemptAt);

        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Queued, null, TestContext.Current.CancellationToken);
        WebhookJobRecord queued = await ReloadAsync(job.Id);
        Assert.Equal(WebhookJobStatus.Queued, queued.Status);
        Assert.Null(queued.NextAttemptAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithoutNextAttempt_KeepsTheSchedule() {
        WebhookJobRecord job = await SavedJobAsync();
        DateTimeOffset next = this.Time.GetUtcNow().AddMinutes(2);
        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Retrying, next, TestContext.Current.CancellationToken);

        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.DeadLettered, TestContext.Current.CancellationToken);

        Assert.Equal(next, (await ReloadAsync(job.Id)).NextAttemptAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownJob_IsIgnored() {
        WebhookJobId unknown = WebhookJobId.NewJobId();

        await this.JobStore.UpdateStatusAsync(unknown, WebhookJobStatus.Delivered, TestContext.Current.CancellationToken);

        Assert.Null(await this.JobStore.GetJobAsync(unknown, TestContext.Current.CancellationToken));
    }

    // ── Stale jobs ───────────────────────────────────────────────────────────

    private Task<IReadOnlyList<WebhookJobRecord>> StaleAsync(DateTimeOffset? inFlight, DateTimeOffset? queued, DateTimeOffset? retrying, int max = 100) {
        return this.JobStore.GetStaleJobsAsync(inFlight, queued, retrying, max, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetStaleJobsAsync_InFlightWithExpiredLease_IsStale() {
        WebhookJobRecord expired = await SavedJobAsync();
        WebhookJobRecord live = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(expired.Id, "worker-a", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        Assert.True(await this.JobStore.TryClaimLeaseAsync(live.Id, "worker-a", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken));

        this.Time.Advance(TimeSpan.FromMinutes(2));
        IReadOnlyList<WebhookJobRecord> stale = await StaleAsync(this.Time.GetUtcNow(), null, null);

        Assert.Equal([expired.Id], stale.Select(j => j.Id));
    }

    [Fact]
    public async Task GetStaleJobsAsync_WithoutInFlightThreshold_ExcludesInFlightJobs() {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "worker-a", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        this.Time.Advance(TimeSpan.FromMinutes(2));

        Assert.Empty(await StaleAsync(null, this.Time.GetUtcNow(), this.Time.GetUtcNow()));
    }

    [Fact]
    public async Task GetStaleJobsAsync_QueuedOlderThanThreshold_IsStale() {
        WebhookJobRecord old = await SavedJobAsync(age: TimeSpan.FromMinutes(10));
        await SavedJobAsync(age: TimeSpan.FromMinutes(1));

        IReadOnlyList<WebhookJobRecord> stale = await StaleAsync(null, this.Time.GetUtcNow().AddMinutes(-5), null);

        Assert.Equal([old.Id], stale.Select(j => j.Id));
    }

    [Fact]
    public async Task GetStaleJobsAsync_QueuedJobHoldingALiveLease_IsNotStale() {
        // Recovery claims a lease and re-queues the job; until that lease expires the job is in hand.
        WebhookJobRecord job = await SavedJobAsync(age: TimeSpan.FromMinutes(10));
        Assert.True(await this.JobStore.TryClaimLeaseAsync(job.Id, "recovery", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));
        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Queued, null, TestContext.Current.CancellationToken);

        DateTimeOffset now = this.Time.GetUtcNow();
        Assert.Empty(await StaleAsync(now, now.AddMinutes(-5), null));

        this.Time.Advance(TimeSpan.FromMinutes(6));
        now = this.Time.GetUtcNow();
        Assert.Equal([job.Id], (await StaleAsync(now, now.AddMinutes(-5), null)).Select(j => j.Id));
    }

    [Fact]
    public async Task GetStaleJobsAsync_RetryingJobWhoseRetryIsDue_IsStale() {
        WebhookJobRecord due = await SavedJobAsync();
        WebhookJobRecord notDue = await SavedJobAsync();
        WebhookJobRecord unscheduled = await SavedJobAsync();
        DateTimeOffset now = this.Time.GetUtcNow();
        await this.JobStore.UpdateStatusAsync(due.Id, WebhookJobStatus.Retrying, now.AddMinutes(-1), TestContext.Current.CancellationToken);
        await this.JobStore.UpdateStatusAsync(notDue.Id, WebhookJobStatus.Retrying, now.AddMinutes(1), TestContext.Current.CancellationToken);
        await this.JobStore.UpdateStatusAsync(unscheduled.Id, WebhookJobStatus.Retrying, null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookJobRecord> stale = await StaleAsync(null, null, now);

        Assert.Equal(new[] { due.Id, unscheduled.Id }.Order(), stale.Select(j => j.Id).Order());
    }

    [Fact]
    public async Task GetStaleJobsAsync_NeverReturnsFinishedJobs() {
        WebhookJobRecord delivered = await SavedJobAsync(age: TimeSpan.FromHours(1));
        WebhookJobRecord deadLettered = await SavedJobAsync(age: TimeSpan.FromHours(1));
        await this.JobStore.UpdateStatusAsync(delivered.Id, WebhookJobStatus.Delivered, TestContext.Current.CancellationToken);
        await this.JobStore.UpdateStatusAsync(deadLettered.Id, WebhookJobStatus.DeadLettered, TestContext.Current.CancellationToken);

        DateTimeOffset future = this.Time.GetUtcNow().AddDays(1);
        Assert.Empty(await StaleAsync(future, future, future));
    }

    [Fact]
    public async Task GetStaleJobsAsync_RespectsMaxCount() {
        for(int i = 0; i < 5; i++) {
            await SavedJobAsync(age: TimeSpan.FromMinutes(10));
        }

        Assert.Equal(3, (await StaleAsync(null, this.Time.GetUtcNow(), null, max: 3)).Count);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => StaleAsync(null, this.Time.GetUtcNow(), null, max: 0));
    }

    // ── Dead letters ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeadLetteredJobsAsync_ReturnsOnlyDeadLetteredJobs() {
        WebhookJobRecord a = await SavedJobAsync("ep-a");
        WebhookJobRecord b = await SavedJobAsync("ep-b");
        WebhookJobRecord delivered = await SavedJobAsync("ep-a");
        await this.JobStore.UpdateStatusAsync(a.Id, WebhookJobStatus.DeadLettered, TestContext.Current.CancellationToken);
        await this.JobStore.UpdateStatusAsync(b.Id, WebhookJobStatus.DeadLettered, TestContext.Current.CancellationToken);
        await this.JobStore.UpdateStatusAsync(delivered.Id, WebhookJobStatus.Delivered, TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookJobRecord> all = await this.JobStore.GetDeadLetteredJobsAsync(10, TestContext.Current.CancellationToken);
        IReadOnlyList<WebhookJobRecord> forA = await this.JobStore.GetDeadLetteredJobsAsync(new WebhookEndpointId("ep-a"), 10, TestContext.Current.CancellationToken);
        IReadOnlyList<WebhookJobRecord> limited = await this.JobStore.GetDeadLetteredJobsAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { a.Id, b.Id }.Order(), all.Select(j => j.Id).Order());
        Assert.Equal([a.Id], forA.Select(j => j.Id));
        Assert.Single(limited);
    }

    // ── Retention ────────────────────────────────────────────────────────────

    /// <summary>
    /// Finishes one job per entry, each <c>ago</c> before the moment the clock is left at. The fake clock only moves
    /// forward, so the oldest is finished first and the clock is advanced between them.
    /// </summary>
    private async Task<WebhookJobRecord[]> FinishedJobsAsync(params (WebhookJobStatus Status, TimeSpan Ago, string Endpoint)[] jobs) {
        DateTimeOffset start = this.Time.GetUtcNow();
        TimeSpan oldest = jobs.Max(j => j.Ago);
        WebhookJobRecord[] records = new WebhookJobRecord[jobs.Length];

        foreach(int i in Enumerable.Range(0, jobs.Length).OrderByDescending(i => jobs[i].Ago)) {
            this.Time.SetUtcNow(start + (oldest - jobs[i].Ago));
            records[i] = await SavedJobAsync(jobs[i].Endpoint);
            await this.JobStore.UpdateStatusAsync(records[i].Id, jobs[i].Status, TestContext.Current.CancellationToken);
        }

        this.Time.SetUtcNow(start + oldest);
        return records;
    }

    [Theory]
    [InlineData(WebhookJobStatus.Delivered)]
    [InlineData(WebhookJobStatus.DeadLettered)]
    public async Task UpdateStatusAsync_ToAFinishedStatus_CompletesAtTheStoresTime(WebhookJobStatus finished) {
        WebhookJobRecord job = await SavedJobAsync();
        Assert.Null((await ReloadAsync(job.Id)).CompletedAt);

        await this.JobStore.UpdateStatusAsync(job.Id, finished, TestContext.Current.CancellationToken);

        Assert.Equal(this.Time.GetUtcNow(), (await ReloadAsync(job.Id)).CompletedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToRetrying_DoesNotComplete() {
        WebhookJobRecord job = await SavedJobAsync();

        await this.JobStore.UpdateStatusAsync(job.Id, WebhookJobStatus.Retrying, this.Time.GetUtcNow().AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Null((await ReloadAsync(job.Id)).CompletedAt);
    }

    [Fact]
    public async Task PruneJobsAsync_DeletesEachFinishedStatusByItsOwnWindow() {
        WebhookJobRecord[] jobs = await FinishedJobsAsync(
            (WebhookJobStatus.Delivered, TimeSpan.FromDays(8), "ep-1"),
            (WebhookJobStatus.Delivered, TimeSpan.FromDays(6), "ep-1"),
            (WebhookJobStatus.DeadLettered, TimeSpan.FromDays(31), "ep-1"),
            (WebhookJobStatus.DeadLettered, TimeSpan.FromDays(8), "ep-1"));
        (WebhookJobRecord oldDelivered, WebhookJobRecord newDelivered, WebhookJobRecord oldDead, WebhookJobRecord newDead) = (jobs[0], jobs[1], jobs[2], jobs[3]);
        DateTimeOffset now = this.Time.GetUtcNow();

        int deleted = await this.JobStore.PruneJobsAsync(now.AddDays(-7), now.AddDays(-30), 100, TestContext.Current.CancellationToken);

        Assert.Equal(2, deleted);
        Assert.Null(await this.JobStore.GetJobAsync(oldDelivered.Id, TestContext.Current.CancellationToken));
        Assert.Null(await this.JobStore.GetJobAsync(oldDead.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await this.JobStore.GetJobAsync(newDelivered.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await this.JobStore.GetJobAsync(newDead.Id, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(WebhookJobStatus.Queued)]
    [InlineData(WebhookJobStatus.InFlight)]
    [InlineData(WebhookJobStatus.Retrying)]
    public async Task PruneJobsAsync_NeverTouchesAJobThatIsNotFinished(WebhookJobStatus active) {
        WebhookJobRecord job = await SavedJobAsync(age: TimeSpan.FromDays(365));
        await this.JobStore.UpdateStatusAsync(job.Id, active, TestContext.Current.CancellationToken);
        DateTimeOffset future = this.Time.GetUtcNow().AddYears(10);

        int deleted = await this.JobStore.PruneJobsAsync(future, future, 100, TestContext.Current.CancellationToken);

        Assert.Equal(0, deleted);
        Assert.NotNull(await this.JobStore.GetJobAsync(job.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PruneJobsAsync_DeletesTheJobsAttemptsAndHistoryWithIt() {
        WebhookJobRecord job = (await FinishedJobsAsync((WebhookJobStatus.Delivered, TimeSpan.FromDays(10), "ep-pruned")))[0];
        await this.JobStore.RecordAttemptAsync(job.Id, WebhookDeliveryAttempt.Create(job.EndpointId, 1, UnixTimestamp.From(this.Time.GetUtcNow()),
            TimeSpan.FromMilliseconds(10), new WebhookDeliveryResult.Delivered(200, "ok")), TestContext.Current.CancellationToken);
        DateTimeOffset now = this.Time.GetUtcNow();

        int deleted = await this.JobStore.PruneJobsAsync(now.AddDays(-7), now.AddDays(-30), 100, TestContext.Current.CancellationToken);

        Assert.Equal(1, deleted);
        Assert.Empty(await this.JobStore.GetHistoryByEndpointAsync(new WebhookEndpointId("ep-pruned"), TestContext.Current.CancellationToken));
        await this.AssertNoAttemptsRemainAsync(job.Id);
    }

    /// <summary>Asserts that no attempt row of <paramref name="jobId"/> remains; a store with no rows of its own has nothing to check.</summary>
    protected virtual Task AssertNoAttemptsRemainAsync(WebhookJobId jobId) {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PruneJobsAsync_DeletesAtMostTheBatchSize_AndDrainsOnTheNextCall() {
        await FinishedJobsAsync([.. Enumerable.Repeat((WebhookJobStatus.Delivered, TimeSpan.FromDays(10), "ep-1"), 5)]);
        DateTimeOffset now = this.Time.GetUtcNow();

        int first = await this.JobStore.PruneJobsAsync(now.AddDays(-7), now.AddDays(-30), 2, TestContext.Current.CancellationToken);
        int second = await this.JobStore.PruneJobsAsync(now.AddDays(-7), now.AddDays(-30), 2, TestContext.Current.CancellationToken);
        int third = await this.JobStore.PruneJobsAsync(now.AddDays(-7), now.AddDays(-30), 2, TestContext.Current.CancellationToken);
        int fourth = await this.JobStore.PruneJobsAsync(now.AddDays(-7), now.AddDays(-30), 2, TestContext.Current.CancellationToken);

        Assert.Equal([2, 2, 1, 0], new[] { first, second, third, fourth });
    }

    [Fact]
    public async Task PruneJobsAsync_InvalidBatchSize_Throws() {
        DateTimeOffset now = this.Time.GetUtcNow();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => this.JobStore.PruneJobsAsync(now, now, 0, TestContext.Current.CancellationToken));
    }
}

public sealed class InMemoryWebhookStoreContractTests : WebhookStoreContractTests {
    protected override StoreBackend CreateBackend() => new InMemoryStoreBackend();
}

public sealed class SqliteWebhookStoreContractTests : WebhookStoreContractTests {
    protected override StoreBackend CreateBackend() => new SqliteStoreBackend();

    protected override Task AssertNoAttemptsRemainAsync(WebhookJobId jobId) => EfCoreAttempts.AssertNoneRemainAsync(this.Services, jobId);
}

public sealed class PostgreSqlWebhookStoreContractTests : WebhookStoreContractTests {
    protected override StoreBackend CreateBackend() => new PostgreSqlStoreBackend();

    protected override Task AssertNoAttemptsRemainAsync(WebhookJobId jobId) => EfCoreAttempts.AssertNoneRemainAsync(this.Services, jobId);
}

internal static class EfCoreAttempts {
    public static async Task AssertNoneRemainAsync(IServiceProvider services, WebhookJobId jobId) {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        TestWebhookDbContext context = scope.ServiceProvider.GetRequiredService<TestWebhookDbContext>();
        string id = jobId.Value;
        Assert.False(await context.Set<WebhookDeliveryAttemptEntity>().AnyAsync(a => a.JobId == id, TestContext.Current.CancellationToken));
    }
}
