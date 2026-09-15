using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Channels;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory.Internal;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

/// <summary>
/// The scheduler's wait must not miss a due time that passes between reading the clock and arming the wait (#148).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "DelayedSchedulerClockRace")]
public sealed class InMemoryDelayedSchedulerClockRaceTests {
    /// <summary>
    /// A fake clock that jumps forward the moment a wait is armed — exactly the window between the scheduler reading
    /// "not due yet" and creating its timer, which a descheduled thread or a jumping clock can open.
    /// </summary>
    private sealed class JumpOnArmTimeProvider(FakeTimeProvider inner, TimeSpan jump) : TimeProvider {
        private int _armed;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override long GetTimestamp() => inner.GetTimestamp();

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            if(Interlocked.Exchange(ref this._armed, 1) == 0) {
                inner.Advance(jump);
            }

            return inner.CreateTimer(callback, state, dueTime, period);
        }
    }

    [Fact]
    public async Task Should_Flush_A_Job_Whose_Due_Time_Passed_While_The_Wait_Was_Being_Armed() {
        Channel<WebhookDeliveryJob> channel = Channel.CreateUnbounded<WebhookDeliveryJob>();
        FakeTimeProvider clock = new();
        JumpOnArmTimeProvider timeProvider = new(clock, TimeSpan.FromSeconds(1));
        await using InMemoryDelayedScheduler scheduler = new(channel.Writer, timeProvider, NullLogger<InMemoryDelayedScheduler>.Instance);

        WebhookDeliveryJob job = WebhookTestFactory.CreateJob();
        scheduler.Schedule(job, TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);

        // Nothing advances the fake clock again: a wait armed after the jump, relative to the jumped time, never fires.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        WebhookDeliveryJob delivered = await channel.Reader.ReadAsync(timeout.Token);

        Assert.Same(job, delivered);
    }
}
