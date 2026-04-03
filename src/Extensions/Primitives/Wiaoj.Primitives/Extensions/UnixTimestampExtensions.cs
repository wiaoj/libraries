using System.Runtime.CompilerServices;

#pragma warning disable IDE0130
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130
/// <summary>
/// Provides convenient extension methods for converting standard .NET time primitives 
/// into <see cref="UnixTimestamp"/> seamlessly and performing domain-specific time operations.
/// </summary>
public static class UnixTimestampExtensions { 
    /// <summary>
    /// Calculates the <see cref="TimeSpan"/> that has elapsed from this timestamp until the current UTC time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimePassed(this UnixTimestamp unixTimestamp) {
        return UnixTimestamp.Now - unixTimestamp;
    }

    /// <summary>
    /// Calculates the elapsed time using a mockable <see cref="TimeProvider"/> for strict unit testing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimePassed(this UnixTimestamp unixTimestamp, TimeProvider timeProvider) {
        return timeProvider.GetUnixTimestamp() - unixTimestamp;
    }

    /// <summary>
    /// Checks if the timestamp is older than the specified duration compared to the current UTC time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOlderThan(this UnixTimestamp unixTimestamp, TimeSpan duration) {
        return unixTimestamp.TimePassed() > duration;
    }

    /// <summary>
    /// Checks if the timestamp is older than the specified duration, using a mockable <see cref="TimeProvider"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOlderThan(this UnixTimestamp unixTimestamp, TimeSpan duration, TimeProvider timeProvider) {
        return unixTimestamp.TimePassed(timeProvider) > duration;
    }

    // -------------------------------------------------------------------------
    // FUTURE TIME HELPERS (GELECEK ZAMAN YARDIMCILARI)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calculates the <see cref="TimeSpan"/> remaining from the current UTC time until this future timestamp.
    /// Useful for checking expiration times (e.g., Cache TTL, Token Expiry).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimeUntil(this UnixTimestamp unixTimestamp) {
        return unixTimestamp - UnixTimestamp.Now;
    }

    /// <summary>
    /// Calculates the remaining time using a mockable <see cref="TimeProvider"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan TimeUntil(this UnixTimestamp unixTimestamp, TimeProvider timeProvider) {
        return unixTimestamp - timeProvider.GetUnixTimestamp();
    }

    // -------------------------------------------------------------------------
    // CONVERSIONS & FACTORIES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the current UTC time from the <see cref="TimeProvider"/> as a <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <param name="timeProvider">The mockable time provider instance.</param>
    /// <returns>A new <see cref="UnixTimestamp"/> representing the current moment.</returns>
    /// <example>
    /// <code>
    /// var expiration = _timeProvider.GetUnixTimestamp().AddDays(7);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp GetUnixTimestamp(this TimeProvider timeProvider) {
        return UnixTimestamp.From(timeProvider);
    }

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> to a <see cref="UnixTimestamp"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp ToUnixTimestamp(this DateTimeOffset dateTimeOffset) {
        return UnixTimestamp.From(dateTimeOffset);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <remarks>
    /// If the <see cref="DateTime.Kind"/> is Local, it will be automatically converted to UTC.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnixTimestamp ToUnixTimestamp(this DateTime dateTime) {
        return UnixTimestamp.From(dateTime);
    }
}