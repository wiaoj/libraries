using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Channels;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;
using Wiaoj.Webhooks.Transports.InMemory.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

/// <summary>
/// The delayed queue holds retries waiting for their backoff, so an outage at the destination fills it. It is bounded,
/// and what happens at the bound is chosen by policy (#42).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "DelayedScheduler")]
public sealed class InMemoryDelayedSchedulerCapacityTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (Channel<WebhookDeliveryJob> Channel, FakeTimeProvider Time, InMemoryDelayedScheduler Scheduler) Create(
        int? capacity,
        DelayedQueueOverflowPolicy policy) {
        Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
        FakeTimeProvider time = new();
        InMemoryDelayedScheduler scheduler = new(
            channel.Writer, time, NullLogger<InMemoryDelayedScheduler>.Instance, capacity, policy);
        return (channel, time, scheduler);
    }

    /// <summary>Waits for the consumer loop, which owns the queue, to reach <paramref name="expected"/> pending jobs.</summary>
    private static async Task WaitForPendingAsync(InMemoryDelayedScheduler scheduler, int expected) {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while(scheduler.PendingCount != expected) {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, Ct);
        }

        Assert.Equal(expected, scheduler.PendingCount);
    }

    private static async Task<List<WebhookJobId>> ReadAllAsync(Channel<WebhookDeliveryJob> channel, int count) {
        List<WebhookJobId> ids = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(Ct, timeout.Token);
        for(int i = 0; i < count; i++) {
            ids.Add((await channel.Reader.ReadAsync(linked.Token)).Id);
        }

        return ids;
    }

    [Fact]
    public async Task PersistOnlyFallback_RefusesTheJob_AndLeavesItToTheStore() {
        (Channel<WebhookDeliveryJob> channel, FakeTimeProvider time, InMemoryDelayedScheduler scheduler) =
            Create(2, DelayedQueueOverflowPolicy.PersistOnlyFallback);
        await using InMemoryDelayedScheduler owned = scheduler;

        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(10), Ct));
        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(10), Ct));
        bool third = scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(10), Ct);

        Assert.False(third);
        Assert.Equal(2, scheduler.PendingCount);

        time.Advance(TimeSpan.FromSeconds(11));
        List<WebhookJobId> flushed = await ReadAllAsync(channel, 2);
        Assert.Equal(2, flushed.Count);
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Reject_Throws_WithTheJobAndTheCapacity() {
        (_, _, InMemoryDelayedScheduler scheduler) = Create(1, DelayedQueueOverflowPolicy.Reject);
        await using InMemoryDelayedScheduler owned = scheduler;
        WebhookDeliveryJob refused = WebhookTestFactory.CreateJob();

        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(10), Ct));
        DelayedQueueFullException error = Assert.Throws<DelayedQueueFullException>(
            () => scheduler.Schedule(refused, TimeSpan.FromSeconds(10), Ct));

        Assert.Equal(refused.Id, error.JobId);
        Assert.Equal(1, error.Capacity);
        Assert.Equal(1, scheduler.PendingCount);
    }

    [Fact]
    public async Task DropOldest_KeepsTheMostUrgentJobs_AndDropsTheOnesDueFurthestAway() {
        (Channel<WebhookDeliveryJob> channel, FakeTimeProvider time, InMemoryDelayedScheduler scheduler) =
            Create(2, DelayedQueueOverflowPolicy.DropOldest);
        await using InMemoryDelayedScheduler owned = scheduler;

        WebhookDeliveryJob inAnHour = WebhookTestFactory.CreateJob();
        WebhookDeliveryJob inTenMinutes = WebhookTestFactory.CreateJob();
        WebhookDeliveryJob inOneMinute = WebhookTestFactory.CreateJob();
        WebhookDeliveryJob inOneSecond = WebhookTestFactory.CreateJob();

        Assert.True(scheduler.Schedule(inAnHour, TimeSpan.FromHours(1), Ct));
        Assert.True(scheduler.Schedule(inTenMinutes, TimeSpan.FromMinutes(10), Ct));
        Assert.True(scheduler.Schedule(inOneMinute, TimeSpan.FromMinutes(1), Ct));
        Assert.True(scheduler.Schedule(inOneSecond, TimeSpan.FromSeconds(1), Ct));

        await WaitForPendingAsync(scheduler, 2);

        time.Advance(TimeSpan.FromHours(2));
        List<WebhookJobId> flushed = await ReadAllAsync(channel, 2);

        Assert.Equal([inOneSecond.Id, inOneMinute.Id], flushed);
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task AFlushedJob_FreesItsSlot() {
        (Channel<WebhookDeliveryJob> channel, FakeTimeProvider time, InMemoryDelayedScheduler scheduler) =
            Create(1, DelayedQueueOverflowPolicy.PersistOnlyFallback);
        await using InMemoryDelayedScheduler owned = scheduler;

        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(5), Ct));
        Assert.False(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(5), Ct));

        time.Advance(TimeSpan.FromSeconds(6));
        await ReadAllAsync(channel, 1);
        await WaitForPendingAsync(scheduler, 0);

        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task ACancelledJob_FreesItsSlot() {
        (_, FakeTimeProvider time, InMemoryDelayedScheduler scheduler) = Create(1, DelayedQueueOverflowPolicy.PersistOnlyFallback);
        await using InMemoryDelayedScheduler owned = scheduler;
        using CancellationTokenSource cancelled = new();

        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(5), cancelled.Token));
        await cancelled.CancelAsync();
        time.Advance(TimeSpan.FromSeconds(6));

        await WaitForPendingAsync(scheduler, 0);
        Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task NoCapacity_AdmitsEverything() {
        (_, _, InMemoryDelayedScheduler scheduler) = Create(null, DelayedQueueOverflowPolicy.Reject);
        await using InMemoryDelayedScheduler owned = scheduler;

        for(int i = 0; i < 50; i++) {
            Assert.True(scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromMinutes(5), Ct));
        }

        Assert.Equal(50, scheduler.PendingCount);
    }

    [Fact]
    public void ACapacityBelowOne_IsRefused() {
        Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();

        Assert.ThrowsAny<ArgumentException>(() => new InMemoryDelayedScheduler(
            channel.Writer,
            TimeProvider.System,
            NullLogger<InMemoryDelayedScheduler>.Instance,
            0,
            DelayedQueueOverflowPolicy.Reject));
    }

    public sealed class TheOptions {
        [Fact]
        public void DefaultToABoundedQueueThatFallsBackToTheStore() {
            InMemoryWebhookTransportOptions options = new();

            Assert.Equal(InMemoryWebhookTransportOptions.DefaultMaxDelayedCapacity, options.MaxDelayedCapacity);
            Assert.Equal(50_000, InMemoryWebhookTransportOptions.DefaultMaxDelayedCapacity);
            Assert.Equal(DelayedQueueOverflowPolicy.PersistOnlyFallback, options.DelayedOverflowPolicy);
        }

        [Fact]
        public void AcceptNullForUnbounded_AndRefuseZeroOrNegative() {
            InMemoryWebhookTransportOptions options = new() { MaxDelayedCapacity = null };
            Assert.Null(options.MaxDelayedCapacity);

            Assert.ThrowsAny<ArgumentException>(() => new InMemoryWebhookTransportOptions { MaxDelayedCapacity = 0 });
            Assert.ThrowsAny<ArgumentException>(() => new InMemoryWebhookTransportOptions { MaxDelayedCapacity = -1 });
        }
    }

    public sealed class TheTransport {
        [Fact]
        public async Task Throws_WhenTheDelayedQueueIsFullUnderReject() {
            using InMemoryWebhookTransport transport = new(new InMemoryWebhookTransportOptions {
                MaxDelayedCapacity = 1,
                DelayedOverflowPolicy = DelayedQueueOverflowPolicy.Reject
            });

            await transport.EnqueueAsync(WebhookTestFactory.CreateJob(), TimeSpan.FromMinutes(5), Ct);

            await Assert.ThrowsAsync<DelayedQueueFullException>(
                () => transport.EnqueueAsync(WebhookTestFactory.CreateJob(), TimeSpan.FromMinutes(5), Ct));
        }

        [Fact]
        public async Task KeepsAcceptingImmediateJobs_WhenTheDelayedQueueIsFull() {
            using InMemoryWebhookTransport transport = new(new InMemoryWebhookTransportOptions {
                MaxDelayedCapacity = 1,
                DelayedOverflowPolicy = DelayedQueueOverflowPolicy.PersistOnlyFallback
            });

            await transport.EnqueueAsync(WebhookTestFactory.CreateJob(), TimeSpan.FromMinutes(5), Ct);
            await transport.EnqueueAsync(WebhookTestFactory.CreateJob(), TimeSpan.FromMinutes(5), Ct);

            WebhookDeliveryJob immediate = WebhookTestFactory.CreateJob();
            await transport.EnqueueAsync(immediate, Ct);

            Assert.True(transport.Reader.TryRead(out WebhookDeliveryJob? read));
            Assert.Equal(immediate.Id, read!.Id);
        }
    }
}
