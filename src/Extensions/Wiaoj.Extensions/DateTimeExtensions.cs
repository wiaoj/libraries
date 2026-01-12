using Wiaoj.Primitives;

namespace Wiaoj.Extensions;
/// <summary>
/// Provides extension methods for <see cref="DateTime"/> and <see cref="DateTimeOffset"/> 
/// to simplify integration with <see cref="UnixTimestamp"/>.
/// </summary>
public static class DateTimeExtensions {
    /// <summary>
    /// Converts the current <see cref="DateTimeOffset"/> to a high-precision <see cref="UnixTimestamp"/>.
    /// </summary>
    /// <param name="dto">The source date time offset.</param>
    /// <returns>A <see cref="UnixTimestamp"/> instance representing the same UTC instant.</returns>
    public static UnixTimestamp ToUnixTimestamp(this DateTimeOffset dto) {
        return UnixTimestamp.From(dto);
    }

    /// <summary>
    /// Converts the current <see cref="DateTime"/> to a high-precision <see cref="UnixTimestamp"/>.
    /// Respects <see cref="DateTimeKind"/> to ensure UTC consistency.
    /// </summary>
    /// <param name="dt">The source date time.</param>
    /// <returns>A <see cref="UnixTimestamp"/> instance.</returns>
    public static UnixTimestamp ToUnixTimestamp(this DateTime dt) {
        return UnixTimestamp.From(dt);
    }
}