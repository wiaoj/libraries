using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Defines the expiration policy for a counter operation.
/// Used to manage TTL (Time-To-Live) for counters in distributed storage.
/// </summary>
public readonly record struct CounterExpiry {

    private readonly TimeSpan? _value;

    /// <summary>
    /// Gets the underlying <see cref="TimeSpan"/> value. 
    /// If <see langword="null"/>, it indicates no expiration or persistent storage.
    /// </summary>
    public TimeSpan? Value => this._value;

    /// <summary>
    /// Gets a value indicating that the counter should never expire.
    /// </summary>
    public static CounterExpiry Infinite { get; } = new(null);

    private CounterExpiry(TimeSpan? value) {
        this._value = value;
    }

    /// <summary>
    /// Creates a <see cref="CounterExpiry"/> from a specific <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="timeSpan">The duration after which the counter should expire.</param>
    /// <returns>A new <see cref="CounterExpiry"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeSpan"/> is non-positive.</exception>
    public static CounterExpiry From(TimeSpan timeSpan) { 
        Preca.ThrowIfNegativeOrZero(timeSpan);
        return new(timeSpan);
    }

    /// <summary>
    /// Creates a <see cref="CounterExpiry"/> from a specified number of seconds.
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    /// <returns>A new <see cref="CounterExpiry"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="seconds"/> is non-positive.</exception>
    public static CounterExpiry FromSeconds(double seconds) {
        return From(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Creates a <see cref="CounterExpiry"/> from a specified number of minutes.
    /// </summary>
    /// <param name="minutes">The duration in minutes.</param>
    /// <returns>A new <see cref="CounterExpiry"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minutes"/> is non-positive.</exception>
    public static CounterExpiry FromMinutes(double minutes) {
        return From(TimeSpan.FromMinutes(minutes));
    }

    /// <summary>
    /// Creates a <see cref="CounterExpiry"/> from a specified number of ticks.
    /// </summary>
    /// <param name="value">The duration in ticks.</param>
    /// <returns>A new <see cref="CounterExpiry"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is non-positive.</exception>
    public static CounterExpiry FromTicks(long value) {
        return From(TimeSpan.FromTicks(value));
    }

    /// <summary>
    /// Gets the total milliseconds of the expiration duration.
    /// Returns 0 if no expiration is set.
    /// </summary>
    /// <returns>The total duration in milliseconds, or 0 if infinite.</returns>
    public long GetTtlMilliseconds() {
        return this.Value?.TotalMilliseconds > 0 ? (long)this.Value.Value.TotalMilliseconds : 0;
    }

    /// <summary>
    /// Implicitly converts a <see cref="TimeSpan"/> to a <see cref="CounterExpiry"/>.
    /// </summary>
    /// <param name="ts">The time span value to convert.</param>
    public static implicit operator CounterExpiry(TimeSpan ts) {
        return From(ts);
    }
}