using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Channels;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "DelayedSchedulerEdgeCases")]
public sealed class InMemoryDelayedSchedulerLockFreeEdgeTests {

    public sealed class ThePreemptionAndInterleaving {
        [Fact]
        public async Task Schedule_WhenNewJobWithEarlierDueTimeArrivesWhileSleeping_PreemptsAndFlushesEarlierJobFirst() {
            // Arrange: Scheduler is sleeping for 2 hours on a late job
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob lateJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("late_2hours"));
            WebhookDeliveryJob urgentJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("urgent_10ms"));

            // 1. Schedule long delay job -> consumer goes to sleep for 2 hours
            scheduler.Schedule(lateJob, TimeSpan.FromHours(2), TestContext.Current.CancellationToken);

            // 2. Schedule urgent job while worker is already sleeping -> must PREEMPT sleep immediately
            scheduler.Schedule(urgentJob, TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);

            // Act: Advance time only 20ms (enough for urgent job, but far before 2 hours)
            timeProvider.Advance(TimeSpan.FromMilliseconds(20));

            // Assert: Urgent job must be flushed immediately; late job must NOT be flushed yet
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(3));
            WebhookDeliveryJob flushed = await channel.Reader.ReadAsync(timeoutCts.Token);
            Assert.Same(urgentJob, flushed);

            // Ensure late job is still waiting
            Assert.False(channel.Reader.TryRead(out _));

            // Advance remaining time -> late job flushes
            timeProvider.Advance(TimeSpan.FromHours(2));
            WebhookDeliveryJob flushedLate = await channel.Reader.ReadAsync(timeoutCts.Token);
            Assert.Same(lateJob, flushedLate);
        }

        [Fact]
        public async Task Schedule_WithZeroOrNegativeDelay_FlushesImmediately() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob zeroDelayJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("zero_delay"));
            WebhookDeliveryJob negativeDelayJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("negative_delay"));

            scheduler.Schedule(zeroDelayJob, TimeSpan.Zero, TestContext.Current.CancellationToken);
            scheduler.Schedule(negativeDelayJob, TimeSpan.FromSeconds(-5), TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromMilliseconds(1));

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(3));
            WebhookDeliveryJob first = await channel.Reader.ReadAsync(timeoutCts.Token);
            WebhookDeliveryJob second = await channel.Reader.ReadAsync(timeoutCts.Token);

            Assert.NotNull(first);
            Assert.NotNull(second);
        }
    }

    public sealed class TheDisposalAndChannelClosure {
        [Fact]
        public async Task Schedule_WhenDestinationChannelIsClosedExternally_SchedulerExitsGracefully() {
            // Arrange: Channel writer completed externally
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            channel.Writer.Complete(); // Channel is closed for writing

            WebhookDeliveryJob job = WebhookTestFactory.CreateJob();
            scheduler.Schedule(job, TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromMilliseconds(20));

            // Wait a brief moment to ensure no uncaught background task exceptions crash the runtime
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Dispose_WhenCalledMultipleTimes_IsIdempotentAndSafe() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            // Double synchronous Dispose
            scheduler.Dispose();
            scheduler.Dispose();

            // Double asynchronous DisposeAsync
            await scheduler.DisposeAsync();
            await scheduler.DisposeAsync();
        }

        [Fact]
        public async Task DisposeAsync_WhenIdleAndQueueIsEmpty_CompletesImmediately() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            // Dispose while waiting on empty inbox (reader.WaitToReadAsync)
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
            await scheduler.DisposeAsync().AsTask().WaitAsync(timeout.Token);
        }
    }
}