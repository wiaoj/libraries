using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Jobs;

/// <summary>
/// A job that reaches workers more than once — a delayed retry and a copy re-enqueued by recovery — is delivered by one
/// copy at a time, and never again once finished (#41).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "JobHandler")]
public sealed class WebhookJobLeaseTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly WebhookEndpointId EndpointId = WebhookTestFactory.CreateEndpointId("lease-endpoint");

    /// <summary>Delivers queued results; the first delivery can be held until released.</summary>
    private sealed class GatedDeliverer(params WebhookDeliveryResult[] results) : IWebhookDeliverer {
        private readonly Queue<WebhookDeliveryResult> _results = new(results);
        private int _calls;

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? ReleaseFirst { get; init; }
        public Func<Task>? DuringDelivery { get; init; }
        public int Calls => Volatile.Read(ref this._calls);

        public async Task<WebhookDeliveryResult> DeliverAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default) {
            int call = Interlocked.Increment(ref this._calls);
            if(this.DuringDelivery is not null) {
                await this.DuringDelivery();
            }

            if(call == 1) {
                this.FirstStarted.TrySetResult();
                if(this.ReleaseFirst is not null) {
                    await this.ReleaseFirst.Task;
                }
            }

            lock(this._results) {
                return this._results.Count > 1 ? this._results.Dequeue() : this._results.Peek();
            }
        }
    }

    private static WebhookJobHandler CreateHandler(
        IWebhookStore store,
        IWebhookDeliverer deliverer,
        TimeProvider timeProvider,
        WebhookJobExecutionGuard? guard = null,
        string instanceId = "pod-a",
        TimeSpan? leaseDuration = null,
        IWebhookEndpointResolver? resolver = null) {
        WebhookPipelineRunner runner = new([], deliverer, timeProvider, NullLogger<WebhookPipelineRunner>.Instance);
        return new WebhookJobHandler(
            store,
            resolver ?? new FakeWebhookEndpointResolver().Register(WebhookTestFactory.CreateEndpoint(EndpointId)),
            new FakeWebhookSerializer(),
            runner,
            timeProvider,
            NullLogger<WebhookJobHandler>.Instance,
            guard ?? new WebhookJobExecutionGuard(),
            Options.Create(new WebhookOptions { InstanceId = instanceId }),
            Options.Create(new WebhookRecoveryOptions { RecoveryLeaseDuration = leaseDuration ?? TimeSpan.FromMinutes(2) }));
    }

    private static async Task<(InMemoryWebhookStore Store, FakeTimeProvider Time, WebhookDeliveryJob Job)> SeedAsync() {
        FakeTimeProvider time = new(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        InMemoryWebhookStore store = new(time);
        WebhookJobId jobId = WebhookJobId.NewJobId();
        await store.SaveAsync(new WebhookJobRecord(jobId, EndpointId, "order.created", "{}", time.GetUtcNow()), Ct);
        return (store, time, WebhookTestFactory.CreateJob(jobId, EndpointId));
    }

    [Fact]
    public async Task HandleAsync_HoldsALeaseForThisInstance_WhileDelivering_AndReleasesItWhenDone() {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        (WebhookJobStatus Status, string? LockedBy, DateTimeOffset? LockExpiresAt)? during = null;
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult()) {
            DuringDelivery = async () => {
                WebhookJobRecord record = (await store.GetJobAsync(job.Id, Ct))!;
                during = (record.Status, record.LockedBy, record.LockExpiresAt);
            }
        };

        await CreateHandler(store, deliverer, time, instanceId: "pod-a", leaseDuration: TimeSpan.FromSeconds(90)).HandleAsync(job, Ct);

        Assert.Equal((WebhookJobStatus.InFlight, "pod-a", time.GetUtcNow().AddSeconds(90)), during);
        WebhookJobRecord after = (await store.GetJobAsync(job.Id, Ct))!;
        Assert.Equal(WebhookJobStatus.Delivered, after.Status);
        Assert.Null(after.LockedBy);
        Assert.Null(after.LockExpiresAt);
    }

    [Fact]
    public async Task HandleAsync_DoesNotDeliver_AJobLeasedByAnotherInstance() {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        Assert.True(await store.TryClaimLeaseAsync(job.Id, "pod-b", TimeSpan.FromMinutes(2), Ct));
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());

        WebhookDeliveryAttempt attempt = await CreateHandler(store, deliverer, time, instanceId: "pod-a").HandleAsync(job, Ct);

        Assert.Equal(0, deliverer.Calls);
        Assert.IsType<WebhookDeliveryResult.Deduplicated>(attempt.Result);
        Assert.Equal(1, attempt.AttemptNumber);
        WebhookJobRecord record = (await store.GetJobAsync(job.Id, Ct))!;
        Assert.Empty(record.Attempts);
        Assert.Equal(WebhookJobStatus.InFlight, record.Status);
        Assert.Equal("pod-b", record.LockedBy);
    }

    [Theory]
    [InlineData(WebhookJobStatus.Delivered)]
    [InlineData(WebhookJobStatus.DeadLettered)]
    public async Task HandleAsync_DoesNotDeliver_AFinishedJobAgain(WebhookJobStatus finished) {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        await store.RecordAttemptAsync(job.Id, WebhookTestFactory.CreateAttempt(1), Ct);
        await store.UpdateStatusAsync(job.Id, finished, Ct);
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());

        WebhookDeliveryAttempt attempt = await CreateHandler(store, deliverer, time).HandleAsync(job, Ct);

        Assert.Equal(0, deliverer.Calls);
        Assert.IsType<WebhookDeliveryResult.Deduplicated>(attempt.Result);
        Assert.Equal(2, attempt.AttemptNumber);
        WebhookJobRecord record = (await store.GetJobAsync(job.Id, Ct))!;
        Assert.Equal(finished, record.Status);
        Assert.Single(record.Attempts);
    }

    [Fact]
    public async Task HandleAsync_TakesOver_OnceAnotherInstancesLeaseHasExpired() {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        Assert.True(await store.TryClaimLeaseAsync(job.Id, "pod-b", TimeSpan.FromMinutes(2), Ct));
        time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());

        await CreateHandler(store, deliverer, time, instanceId: "pod-a").HandleAsync(job, Ct);

        Assert.Equal(1, deliverer.Calls);
        Assert.Equal(WebhookJobStatus.Delivered, (await store.GetJobAsync(job.Id, Ct))!.Status);
    }

    [Fact]
    public async Task HandleAsync_DeliversOnce_WhenTwoCopiesArriveConcurrentlyOnTheSameInstance() {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        WebhookJobExecutionGuard guard = new();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult()) { ReleaseFirst = release };

        Task<WebhookDeliveryAttempt> first = CreateHandler(store, deliverer, time, guard).HandleAsync(job, Ct);
        await deliverer.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Task<WebhookDeliveryAttempt> second = CreateHandler(store, deliverer, time, guard).HandleAsync(job, Ct);

        await Task.Delay(100, Ct);
        Assert.False(second.IsCompleted); // waits for the first copy instead of delivering alongside it
        release.SetResult();

        WebhookDeliveryAttempt[] attempts = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Equal(1, deliverer.Calls);
        Assert.True(attempts[0].IsSuccess);
        Assert.IsType<WebhookDeliveryResult.Deduplicated>(attempts[1].Result);
        Assert.Single((await store.GetJobAsync(job.Id, Ct))!.Attempts);
    }

    [Fact]
    public async Task HandleAsync_StillDelivers_ARetryThatArrivesBeforeTheAttemptSchedulingItFinishes() {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        WebhookJobExecutionGuard guard = new();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GatedDeliverer deliverer = new(
            WebhookTestFactory.CreateTransientFailureResult(),
            WebhookTestFactory.CreateSuccessResult()) { ReleaseFirst = release };

        Task<WebhookDeliveryAttempt> failing = CreateHandler(store, deliverer, time, guard).HandleAsync(job, Ct);
        await deliverer.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        // A retry with no delay (Retry-After: 0) reaches a worker while the failing attempt is still finishing.
        Task<WebhookDeliveryAttempt> retry = CreateHandler(store, deliverer, time, guard).HandleAsync(job, Ct);
        release.SetResult();

        await Task.WhenAll(failing, retry).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Equal(2, deliverer.Calls);
        Assert.True((await retry).IsSuccess);
        WebhookJobRecord record = (await store.GetJobAsync(job.Id, Ct))!;
        Assert.Equal(WebhookJobStatus.Delivered, record.Status);
        Assert.Equal(2, record.Attempts.Count);
    }

    [Fact]
    public async Task HandleAsync_Delivers_AJobTheStoreDoesNotTrack() {
        FakeTimeProvider time = new();
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult());

        WebhookDeliveryAttempt attempt = await CreateHandler(new InMemoryWebhookStore(time), deliverer, time)
            .HandleAsync(WebhookTestFactory.CreateJob(WebhookJobId.NewJobId(), EndpointId), Ct);

        Assert.True(attempt.IsSuccess);
        Assert.Equal(1, deliverer.Calls);
    }

    [Fact]
    public async Task HandleAsync_ReleasesTheGuard_WhenDeliveryThrows() {
        (InMemoryWebhookStore store, FakeTimeProvider time, WebhookDeliveryJob job) = await SeedAsync();
        WebhookJobExecutionGuard guard = new();
        WebhookJobHandler unresolvable = CreateHandler(
            store, new GatedDeliverer(WebhookTestFactory.CreateSuccessResult()), time, guard, resolver: new FakeWebhookEndpointResolver());

        await Assert.ThrowsAsync<WebhookEndpointNotFoundException>(() => unresolvable.HandleAsync(job, Ct));

        using WebhookJobExecutionGuard.Releaser slot = await guard.EnterAsync(job.Id, Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public async Task HandlersResolvedFromTheContainer_ShareOneGuard() {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GatedDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult()) { ReleaseFirst = release };
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ISerializer<WebhookSerializerKey>, FakeWebhookSerializer>();
        services.AddSingleton<IWebhookEndpointResolver>(new FakeWebhookEndpointResolver().Register(WebhookTestFactory.CreateEndpoint(EndpointId)));
        services.AddSingleton<IWebhookDeliverer>(deliverer);
        services.AddWiaojWebhooks();
        await using ServiceProvider provider = services.BuildServiceProvider();

        IWebhookStore store = provider.GetRequiredService<IWebhookStore>();
        WebhookJobId jobId = WebhookJobId.NewJobId();
        await store.SaveAsync(new WebhookJobRecord(jobId, EndpointId, "order.created", "{}", DateTimeOffset.UtcNow), Ct);
        WebhookDeliveryJob job = WebhookTestFactory.CreateJob(jobId, EndpointId);

        // Handlers are transient; both copies must still take turns on the same job.
        Task<WebhookDeliveryAttempt> first = provider.GetRequiredService<IWebhookJobHandler>().HandleAsync(job, Ct);
        await deliverer.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Task<WebhookDeliveryAttempt> second = provider.GetRequiredService<IWebhookJobHandler>().HandleAsync(job, Ct);
        await Task.Delay(100, Ct);
        bool secondWaited = !second.IsCompleted;
        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.True(secondWaited);
        Assert.Equal(1, deliverer.Calls);
        Assert.Same(provider.GetRequiredService<WebhookJobExecutionGuard>(), provider.GetRequiredService<WebhookJobExecutionGuard>());
    }

    [Fact]
    public async Task Guard_StopsWaiting_WhenCancelled() {
        WebhookJobExecutionGuard guard = new();
        WebhookJobId jobId = WebhookJobId.NewJobId();
        using WebhookJobExecutionGuard.Releaser held = await guard.EnterAsync(jobId, Ct);
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => guard.EnterAsync(jobId, cts.Token).AsTask());
    }

    [Fact]
    public async Task Guard_LetsDifferentJobsRunConcurrently() {
        WebhookJobExecutionGuard guard = new();
        using WebhookJobExecutionGuard.Releaser first = await guard.EnterAsync(WebhookJobId.NewJobId(), Ct);

        using WebhookJobExecutionGuard.Releaser second = await guard.EnterAsync(WebhookJobId.NewJobId(), Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }
}
