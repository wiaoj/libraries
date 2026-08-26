using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Fakes;

public sealed class ClockSkewTimeProvider(FakeTimeProvider inner) : TimeProvider {
    public TimeSpan WallClockOffset { get; set; } = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() {
        return inner.GetUtcNow() + this.WallClockOffset;
    }

    public override long GetTimestamp() {
        return inner.GetTimestamp();
    }

    public override long TimestampFrequency => inner.TimestampFrequency;
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        return inner.CreateTimer(callback, state, dueTime, period);
    }
}