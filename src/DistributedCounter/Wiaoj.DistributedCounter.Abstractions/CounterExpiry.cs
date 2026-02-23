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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeSpan"/> is non-positive.</exception>
    public static CounterExpiry From(TimeSpan timeSpan) {
        if(timeSpan <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeSpan), "Expiry must be positive.");
        return new(timeSpan);
    }

    /// <summary>
    /// Creates a <see cref="CounterExpiry"/> from a specified number of seconds.
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    public static CounterExpiry FromSeconds(double seconds) {
        return From(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Creates a <see cref="CounterExpiry"/> from a specified number of minutes.
    /// </summary>
    /// <param name="minutes">The duration in minutes.</param>
    public static CounterExpiry FromMinutes(double minutes) {
        return From(TimeSpan.FromMinutes(minutes));
    }

    public static CounterExpiry FromTicks(long value) {
        return From(TimeSpan.FromTicks(value));
    }

    /// <summary>
    /// Gets the total milliseconds of the expiration duration.
    /// Returns 0 if no expiration is set.
    /// </summary>
    public long GetTtlMilliseconds() {
        return Value?.TotalMilliseconds > 0 ? (long)Value.Value.TotalMilliseconds : 0;
    }

    /// <summary>
    /// Implicitly converts a <see cref="TimeSpan"/> to a <see cref="CounterExpiry"/>.
    /// </summary>
    public static implicit operator CounterExpiry(TimeSpan ts) {
        return From(ts);
    }
}