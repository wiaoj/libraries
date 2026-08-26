using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.Webhooks.Tests.Unit.Fakes;

/// <summary>
/// A test fake that wraps <see cref="FakeTimeProvider"/> to simulate independent wall-clock drift
/// while maintaining accurate monotonic timer progression.
/// </summary>
internal sealed class ClockSkewTimeProvider(FakeTimeProvider inner) : TimeProvider {
    /// <summary>
    /// Gets or sets the offset applied to <see cref="GetUtcNow"/> to simulate clock drift.
    /// </summary>
    public TimeSpan WallClockOffset { get; set; } = TimeSpan.Zero;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() {
        return inner.GetUtcNow() + this.WallClockOffset;
    }

    /// <inheritdoc/>
    public override long GetTimestamp() {
        return inner.GetTimestamp();
    }

    /// <inheritdoc/>
    public override long TimestampFrequency => inner.TimestampFrequency;

    /// <inheritdoc/>
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        return inner.CreateTimer(callback, state, dueTime, period);
    }
}