using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

public sealed class InMemoryDelayedSchedulerDataLossTests {
    [Fact]
    public async Task Schedule_WhenBoundedChannelIsFullAtTimerExpiry_ShouldNotDropMessage() {
        // Arrange: Configure bounded channel with capacity of 1 to simulate backpressure
        Channel<WebhookDeliveryJob> channel = Channel.CreateBounded<WebhookDeliveryJob>(new BoundedChannelOptions(1) {
            FullMode = BoundedChannelFullMode.Wait
        });

        FakeTimeProvider timeProvider = new();
        using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

        // Saturate the channel capacity with an initial blocking job
        WebhookDeliveryJob blockingJob = WebhookTestFactory.CreateJob();
        await channel.Writer.WriteAsync(blockingJob, TestContext.Current.CancellationToken);

        // Schedule delayed job to trigger while channel is saturated
        WebhookDeliveryJob delayedJob = WebhookTestFactory.CreateJob();
        scheduler.Schedule(delayedJob, TimeSpan.FromMilliseconds(30), CancellationToken.None);

        // Act: Advance fake clock past the delay threshold and wait for the scheduler loop to process
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        await Task.Delay(20, TestContext.Current.CancellationToken);

        // Drain the initial blocking job to free up channel capacity
        bool readFirst = channel.Reader.TryRead(out WebhookDeliveryJob? dequeuedFirst);
        Assert.True(readFirst);
        Assert.Same(blockingJob, dequeuedFirst);

        // Wait briefly for the suspended WriteAsync to complete now that capacity is available
        await Task.Delay(20, TestContext.Current.CancellationToken);

        // Assert: Delayed job must be queued successfully once capacity is freed
        bool readDelayed = channel.Reader.TryRead(out WebhookDeliveryJob? dequeuedDelayed);
        Assert.True(readDelayed, "Delayed job was dropped because channel buffer was saturated at timer expiration.");
        Assert.Same(delayedJob, dequeuedDelayed);
    }

    [Fact]
    public async Task Schedule_MultipleDelayedJobs_FlushesInChronologicalDueOrder() {
        // Arrange
        Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
        FakeTimeProvider timeProvider = new();
        using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

        WebhookDeliveryJob earlyJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("early"));
        WebhookDeliveryJob lateJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("late"));

        // Schedule late job first (80ms), then early job (20ms)
        scheduler.Schedule(lateJob, TimeSpan.FromMilliseconds(80), CancellationToken.None);
        scheduler.Schedule(earlyJob, TimeSpan.FromMilliseconds(20), CancellationToken.None);

        // Act: Advance fake time past both job thresholds
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await Task.Delay(20, TestContext.Current.CancellationToken);

        // Assert: Early job must be flushed into the channel before the late job
        bool readFirst = channel.Reader.TryRead(out WebhookDeliveryJob? first);
        bool readSecond = channel.Reader.TryRead(out WebhookDeliveryJob? second);

        Assert.True(readFirst);
        Assert.True(readSecond);
        Assert.Same(earlyJob, first);
        Assert.Same(lateJob, second);
    }

    [Fact]
    public async Task Dispose_CancelsPendingDelayedJobs_WithoutFlushingToChannel() {
        // Arrange
        Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
        FakeTimeProvider timeProvider = new();
        InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

        WebhookDeliveryJob job = WebhookTestFactory.CreateJob();
        scheduler.Schedule(job, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        // Act: Dispose scheduler before delay expires
        scheduler.Dispose();

        // Advance time past the scheduled point
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        await Task.Delay(20, TestContext.Current.CancellationToken);

        // Assert: Channel remains empty because scheduler was disposed
        Assert.False(channel.Reader.TryRead(out _));
    }
}