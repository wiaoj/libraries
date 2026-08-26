using System.Runtime.CompilerServices;

#pragma warning disable IDE0130
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130

/// <summary>
/// Provides convenient extension methods for converting standard .NET time providers 
/// into <see cref="MonotonicTimestamp"/> and performing monotonic domain operations.
/// </summary>
public static class MonotonicTimestampExtensions {
    /// <summary>
    /// Calculates the <see cref="TimeSpan"/> that has elapsed from this timestamp until the current monotonic instant.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimePassed(this MonotonicTimestamp timestamp) {
        return MonotonicTimestamp.Now - timestamp;
    }

    /// <summary>
    /// Calculates the elapsed duration using a mockable <see cref="TimeProvider"/> for unit testing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimePassed(this MonotonicTimestamp timestamp, TimeProvider timeProvider) {
        return timeProvider.GetMonotonicTimestamp() - timestamp;
    }

    /// <summary>
    /// Checks if the timestamp is older than the specified duration compared to the current monotonic time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOlderThan(this MonotonicTimestamp timestamp, TimeSpan duration) {
        return timestamp.TimePassed() > duration;
    }

    /// <summary>
    /// Checks if the timestamp is older than the specified duration, using a mockable <see cref="TimeProvider"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOlderThan(this MonotonicTimestamp timestamp, TimeSpan duration, TimeProvider timeProvider) {
        return timestamp.TimePassed(timeProvider) > duration;
    }

    // -------------------------------------------------------------------------
    // FUTURE TIME HELPERS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calculates the remaining <see cref="TimeSpan"/> until this future timestamp expires.
    /// Returns <see cref="TimeSpan.Zero"/> if the timestamp has already passed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimeUntil(this MonotonicTimestamp timestamp) {
        MonotonicTimestamp now = MonotonicTimestamp.Now;
        return timestamp > now ? timestamp - now : TimeSpan.Zero;
    }

    /// <summary>
    /// Calculates the remaining time until this future timestamp expires, using a mockable <see cref="TimeProvider"/>.
    /// Returns <see cref="TimeSpan.Zero"/> if the timestamp has already passed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimeUntil(this MonotonicTimestamp timestamp, TimeProvider timeProvider) {
        MonotonicTimestamp now = timeProvider.GetMonotonicTimestamp();
        return timestamp > now ? timestamp - now : TimeSpan.Zero;
    }

    // -------------------------------------------------------------------------
    // FACTORIES & CONVERSIONS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the current high-resolution monotonic timestamp from the <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The mockable time provider instance.</param>
    /// <returns>A new <see cref="MonotonicTimestamp"/> representing the current monotonic instant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonotonicTimestamp GetMonotonicTimestamp(this TimeProvider timeProvider) {
        return MonotonicTimestamp.From(timeProvider);
    }
}