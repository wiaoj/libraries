using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.Testing;

/// <summary>
/// A deterministic, fake <see cref="IRateLimitAlgorithm"/> test double intended for unit
/// testing consumers of rate limiting (e.g. webhook adapters, ASP.NET Core controllers)
/// without real storage or delays.
/// </summary>
public sealed class FakeRateLimitAlgorithm : IRateLimitAlgorithm {
    private readonly TimeProvider _timeProvider;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, WindowState> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new fake rate limit algorithm test double using the system clock.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed per key within a single window. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public FakeRateLimitAlgorithm(
        int limit,
        TimeSpan window)
        : this(limit, window, TimeProvider.System) { }

    /// <summary>
    /// Creates a new fake rate limit algorithm test double with a custom time provider.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed per key within a single window. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving window boundaries.</param>
    public FakeRateLimitAlgorithm(
        int limit,
        TimeSpan window,
        TimeProvider timeProvider) {
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);
        Preca.ThrowIfNull(timeProvider);

        this._limit = limit;
        this._window = window;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        bool allowed = false;
        WindowState updated = this._state.AddOrUpdate(
            key,
            addValueFactory: _ => {
                allowed = true;
                return new WindowState(now + this._window, cost);
            },
            updateValueFactory: (_, existing) => {
                if(now >= existing.ResetAt) {
                    allowed = true;
                    return new WindowState(now + this._window, cost);
                }
                if(existing.Used + cost > this._limit) {
                    allowed = false;
                    return existing;
                }
                allowed = true;
                return existing with { Used = existing.Used + cost };
            });

        if(!allowed) {
            TimeSpan retryAfter = updated.ResetAt - now;
            return ValueTask.FromResult(RateLimitDecision.Denied(
                retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter,
                remaining: 0));
        }

        return ValueTask.FromResult(RateLimitDecision.Allowed(remaining: this._limit - updated.Used));
    }

    /// <summary>Clears all tracked state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct WindowState(DateTimeOffset ResetAt, long Used);
}