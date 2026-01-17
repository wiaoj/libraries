using Wiaoj.Primitives;

namespace Wiaoj.Extensions;
public static class TimeProviderExtensions {
    /// <summary>
    /// Gets the current UTC time as a <see cref="UnixTimestamp"/>.
    /// </summary>
    public static UnixTimestamp GetUnixTimestamp(this TimeProvider timeProvider) {
        return UnixTimestamp.From(timeProvider.GetUtcNow());
    }
}