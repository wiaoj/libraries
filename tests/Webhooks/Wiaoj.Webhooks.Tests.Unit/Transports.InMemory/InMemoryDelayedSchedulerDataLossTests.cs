using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "DelayedScheduler")]
public sealed class InMemoryDelayedSchedulerTests {

    public sealed class TheConstructorValidation {
        [Fact]
        public void GivenNullWriter_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new InMemoryDelayedScheduler(null!, TimeProvider.System, NullLogger<InMemoryDelayedScheduler>.Instance));
        }

        [Fact]
        public void GivenNullTimeProvider_ThrowsArgumentNullException() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new InMemoryDelayedScheduler(channel.Writer, null!, NullLogger<InMemoryDelayedScheduler>.Instance));
        }

        [Fact]
        public void GivenNullLogger_ThrowsArgumentNullException() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            Assert.ThrowsAny<ArgumentNullException>(() =>
                new InMemoryDelayedScheduler(channel.Writer, TimeProvider.System, null!));
        }
    }

    public sealed class TheBackpressureAndDataLoss {
        [Fact]
        public async Task Schedule_WhenBoundedChannelIsFullAtTimerExpiry_ShouldNotDropMessage() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateBounded<WebhookDeliveryJob>(new BoundedChannelOptions(1) {
                FullMode = BoundedChannelFullMode.Wait
            });

            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob blockingJob = WebhookTestFactory.CreateJob();
            await channel.Writer.WriteAsync(blockingJob, TestContext.Current.CancellationToken);

            WebhookDeliveryJob delayedJob = WebhookTestFactory.CreateJob();
            scheduler.Schedule(delayedJob, TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromMilliseconds(50));

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(5));
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, timeoutCts.Token);

            WebhookDeliveryJob dequeuedFirst = await channel.Reader.ReadAsync(linkedCts.Token);
            Assert.Same(blockingJob, dequeuedFirst);

            WebhookDeliveryJob dequeuedDelayed = await channel.Reader.ReadAsync(linkedCts.Token);
            Assert.Same(delayedJob, dequeuedDelayed);
        }
    }

    public sealed class TheOrderingAndExecution {
        [Fact]
        public async Task Schedule_MultipleDelayedJobs_FlushesInChronologicalDueOrder() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob earlyJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("early"));
            WebhookDeliveryJob lateJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("late"));

            scheduler.Schedule(lateJob, TimeSpan.FromMilliseconds(80), TestContext.Current.CancellationToken);
            scheduler.Schedule(earlyJob, TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromMilliseconds(100));

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(5));
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, timeoutCts.Token);

            WebhookDeliveryJob first = await channel.Reader.ReadAsync(linkedCts.Token);
            WebhookDeliveryJob second = await channel.Reader.ReadAsync(linkedCts.Token);

            Assert.Same(earlyJob, first);
            Assert.Same(lateJob, second);
        }

        [Fact]
        public async Task Schedule_WhenJobCancellationTokenIsCancelled_DiscardsJobSilently() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            using CancellationTokenSource jobCts = new();
            WebhookDeliveryJob job = WebhookTestFactory.CreateJob();

            scheduler.Schedule(job, TimeSpan.FromMilliseconds(30), jobCts.Token);

            jobCts.Cancel();
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));

            await Task.Delay(25, TestContext.Current.CancellationToken);
            Assert.False(channel.Reader.TryRead(out _));
        }
    }

    public sealed class TheDisposalAndCancellation {
        [Fact]
        public async Task Dispose_CancelsPendingDelayedJobs_WithoutFlushingToChannel() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob job = WebhookTestFactory.CreateJob();
            scheduler.Schedule(job, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

            scheduler.Dispose();

            timeProvider.Advance(TimeSpan.FromMilliseconds(150));
            await Task.Delay(25, TestContext.Current.CancellationToken);

            Assert.False(channel.Reader.TryRead(out _));
        }

        [Fact]
        public void Schedule_WhenDisposed_ThrowsObjectDisposedException() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            InMemoryDelayedScheduler scheduler = new(channel.Writer, TimeProvider.System, NullLogger<InMemoryDelayedScheduler>.Instance);

            scheduler.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        }
    }
}