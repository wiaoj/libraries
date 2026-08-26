using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory.Internal;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "DelayedSchedulerAdversarial")]
public sealed class InMemoryDelayedSchedulerAdversarialChaosTests {

    public sealed class TheNtpClockSkewAndTimeWarp {
        [Fact]
        public async Task Schedule_WhenSystemClockLeapsForwardMassively_FlushesAllPendingJobsWithoutHanging() {
            // Arrange: Simulate massive time warp leap (e.g. system wake from sleep or large NTP forward jump)
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            DateTimeOffset initialTime = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(initialTime);
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob job1 = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("job_1"));
            WebhookDeliveryJob job2 = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("job_2"));

            scheduler.Schedule(job1, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
            scheduler.Schedule(job2, TimeSpan.FromHours(2), TestContext.Current.CancellationToken);

            // Act: Time leaps forward by an entire day (24 hours) in a single jump
            timeProvider.Advance(TimeSpan.FromDays(1));

            // Assert: Both pending jobs must be flushed to the channel immediately without deadlock
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(3));
            WebhookDeliveryJob first = await channel.Reader.ReadAsync(timeoutCts.Token);
            WebhookDeliveryJob second = await channel.Reader.ReadAsync(timeoutCts.Token);

            Assert.Same(job1, first);
            Assert.Same(job2, second);
        }

        [Fact]
        public async Task Schedule_WhenSystemClockLeapsForward_FlushesJobsInDueTimeOrder_RegardlessOfScheduleOrder() {
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            DateTimeOffset initialTime = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(initialTime);
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob laterJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("later"));
            WebhookDeliveryJob earlierJob = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("earlier"));

            // Schedule the LATER-due job first, to decouple registration order from due-time order
            scheduler.Schedule(laterJob, TimeSpan.FromHours(2), TestContext.Current.CancellationToken);
            scheduler.Schedule(earlierJob, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromDays(1));

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(3));
            WebhookDeliveryJob first = await channel.Reader.ReadAsync(timeoutCts.Token);
            WebhookDeliveryJob second = await channel.Reader.ReadAsync(timeoutCts.Token);

            Assert.Same(earlierJob, first);
            Assert.Same(laterJob, second);
        }

        [Fact]
        public async Task Schedule_WhenNtpPullsSystemClockBackward_OldFails_NewFlushesOnTime() {
            // Arrange
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();

            FakeTimeProvider fakeTime = new();
            ClockSkewTimeProvider timeProvider = new(fakeTime);

            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob job = WebhookTestFactory.CreateJob();

            scheduler.Schedule(job, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            timeProvider.WallClockOffset = TimeSpan.FromHours(-1);

            fakeTime.Advance(TimeSpan.FromSeconds(10));

            // Assert
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(2));
            WebhookDeliveryJob delivered = await channel.Reader.ReadAsync(timeoutCts.Token);

            Assert.Same(job, delivered);
        }

        /// <summary>
        /// Wraps FakeTimeProvider to simulate real-world NTP wall-clock drift/skew independently of monotonic timers.
        /// </summary>
        private sealed class ClockSkewTimeProvider(FakeTimeProvider inner) : TimeProvider {
            public TimeSpan WallClockOffset { get; set; } = TimeSpan.Zero;

            public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + this.WallClockOffset;
            public override long GetTimestamp() => inner.GetTimestamp();
            public override long TimestampFrequency => inner.TimestampFrequency;
            public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
                => inner.CreateTimer(callback, state, dueTime, period);
        }
    }

    public sealed class TheExtremeConcurrencyAndHeapStress {
        [Fact]
        public async Task Schedule_1000ConcurrentThreadsFloodingDifferentDelays_FlushesAllWithoutLossOrDeadlock() {
            // Arrange: 1,000 producer tasks simultaneously scheduling jobs with variable delay intervals
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            FakeTimeProvider timeProvider = new();
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            const int totalJobs = 1000;
            ConcurrentDictionary<string, WebhookDeliveryJob> scheduledJobs = new(StringComparer.Ordinal);

            // Act: Dispatch concurrent scheduling flood
            Task[] producers = [.. Enumerable.Range(0, totalJobs).Select(i => Task.Run(() => {
                WebhookDeliveryJob job = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId($"ep_{i}"));
                scheduledJobs[job.Id.Value] = job;
                TimeSpan randomDelay = TimeSpan.FromMilliseconds((i % 50) + 10);
                scheduler.Schedule(job, randomDelay, TestContext.Current.CancellationToken);
            }))];

            await Task.WhenAll(producers);

            // Advance clock past the maximum possible delay window
            timeProvider.Advance(TimeSpan.FromMilliseconds(500));

            // Assert: Exactly 1,000 unique jobs must be flushed to the channel without data loss
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
            int receivedCount = 0;

            while(receivedCount < totalJobs && await channel.Reader.WaitToReadAsync(timeoutCts.Token)) {
                while(channel.Reader.TryRead(out WebhookDeliveryJob? dequeued)) {
                    Assert.NotNull(dequeued);
                    Assert.True(scheduledJobs.ContainsKey(dequeued.Id.Value));
                    receivedCount++;
                }
            }

            Assert.Equal(totalJobs, receivedCount);
        }
    }

    public sealed class TheGracefulShutdownUnderBackpressure {
        [Fact]
        public async Task DisposeAsync_WhileWorkerIsBlockedOnFullChannel_UnblocksAndCompletesGracefully() {
            // Arrange: Bounded channel with capacity of 1 saturated by an initial job
            Channel<WebhookDeliveryJob> channel = Channel.CreateBounded<WebhookDeliveryJob>(new BoundedChannelOptions(1) {
                FullMode = BoundedChannelFullMode.Wait
            });

            FakeTimeProvider timeProvider = new();
            InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            await channel.Writer.WriteAsync(WebhookTestFactory.CreateJob(), TestContext.Current.CancellationToken);

            // Schedule delayed job and advance time to make worker block on WriteAsync
            scheduler.Schedule(WebhookTestFactory.CreateJob(), TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromMilliseconds(20));

            // Act & Assert: DisposeAsync must cancel background write and complete without hanging
            using CancellationTokenSource shutdownTimeout = new(TimeSpan.FromSeconds(3));
            ValueTask disposeTask = scheduler.DisposeAsync();

            await disposeTask.AsTask().WaitAsync(shutdownTimeout.Token);
            Assert.True(disposeTask.IsCompleted);
        }
    }

    public sealed class TheEdgeCaseTimingBehaviors {
        [Fact]
        public async Task Schedule_WhenTwoJobsShareIdenticalDueTime_FlushesBothWithoutLoss() {
            // Arrange: Two jobs scheduled with the exact same delay, landing on the identical due timestamp.
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            DateTimeOffset initialTime = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(initialTime);
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob jobA = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("tie_a"));
            WebhookDeliveryJob jobB = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("tie_b"));

            scheduler.Schedule(jobA, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);
            scheduler.Schedule(jobB, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

            // Act: Advance exactly to the shared due time.
            timeProvider.Advance(TimeSpan.FromMinutes(15));

            // Assert: Both jobs must surface — neither may be lost, overwritten, or coalesced due to a key collision.
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(3));
            WebhookDeliveryJob first = await channel.Reader.ReadAsync(timeoutCts.Token);
            WebhookDeliveryJob second = await channel.Reader.ReadAsync(timeoutCts.Token);

            HashSet<WebhookDeliveryJob> delivered = [first, second];
            Assert.Contains(jobA, delivered);
            Assert.Contains(jobB, delivered);
            Assert.Equal(2, delivered.Count);
        }

        [Fact]
        public async Task Schedule_WhenClockAdvancesToExactlyTheDueTime_FlushesJobImmediately() {
            // Arrange: Targets an exact boundary condition (now >= dueTime).
            Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
            DateTimeOffset initialTime = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(initialTime);
            await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

            WebhookDeliveryJob job = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("exact_boundary"));

            scheduler.Schedule(job, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

            // Act: Advance by precisely the scheduled delay — no more, no less.
            timeProvider.Advance(TimeSpan.FromMinutes(5));

            // Assert: The job must be considered due at the exact boundary, not strictly after it.
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(3));
            WebhookDeliveryJob delivered = await channel.Reader.ReadAsync(timeoutCts.Token);

            Assert.Same(job, delivered);
        }
    }
}