using System.Collections.Concurrent;

namespace Wiaoj.RateLimiting.Testing;

/// <summary>
/// A deterministic, in-memory <see cref="IRateLimitAlgorithm"/> implementation intended for unit
/// tests of <em>consumers</em> of rate limiting (e.g. a webhook adapter, an ASP.NET Core middleware) —
/// not for testing the algorithms themselves.
/// </summary>
/// <remarks>
/// <para>
/// This is a fixed-window emulation deliberately kept as simple as possible: no partial-window
/// weighting, no burst smoothing. If you need to test sliding-window or token-bucket specific
/// behavior, exercise the real <c>SlidingWindowRateLimiter</c> / token bucket implementation with a
/// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> instead — this type is for
/// consumers who just need "some algorithm that behaves predictably" as a collaborator.
/// </para>
/// <para>
/// Time is driven entirely by the injected <see cref="TimeProvider"/>. Use
/// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> and call
/// <c>Advance(...)</c> between assertions to move windows forward without real delays.
/// </para>
/// </remarks>
public sealed class InMemoryRateLimitAlgorithm : IRateLimitAlgorithm {
    private readonly TimeProvider _timeProvider;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, WindowState> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new fixed-window in-memory algorithm.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed per key within a single window. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">
    /// The time provider driving window boundaries. Pass a
    /// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> in tests;
    /// defaults to <see cref="TimeProvider.System"/> when omitted.
    /// </param>
    public InMemoryRateLimitAlgorithm(int limit, TimeSpan window, TimeProvider? timeProvider = null) {
        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }
        if (window <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be greater than zero.");
        }

        this._limit = limit;
        this._window = window;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        // Single atomic pass: the factory decides, per invocation, whether this attempt fits
        // within the (possibly just-reset) window. ConcurrentDictionary guarantees the value
        // ultimately stored/returned corresponds to the invocation that "won" the CAS race, so
        // capturing the decision in a closure variable and reading it after AddOrUpdate returns
        // is safe — no separate rollback step, no window where a denied attempt is briefly counted.
        bool allowed = false;
        WindowState updated = this._state.AddOrUpdate(
            key,
            addValueFactory: _ => {
                allowed = true;
                return new WindowState(now + this._window, cost);
            },
            updateValueFactory: (_, existing) => {
                if (now >= existing.ResetAt) {
                    // Window has elapsed — start a fresh one.
                    allowed = true;
                    return new WindowState(now + this._window, cost);
                }
                if (existing.Used + cost > this._limit) {
                    allowed = false;
                    return existing; // unchanged — denied attempts never mutate state
                }
                allowed = true;
                return existing with { Used = existing.Used + cost };
            });

        if (!allowed) {
            TimeSpan retryAfter = updated.ResetAt - now;
            return ValueTask.FromResult(RateLimitDecision.Denied(
                retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter,
                remaining: 0));
        }

        return ValueTask.FromResult(RateLimitDecision.Allowed(remaining: this._limit - updated.Used));
    }

    /// <summary>Clears all tracked state. Useful between test cases if the instance is reused.</summary>
    public void Reset() => this._state.Clear();

    private readonly record struct WindowState(DateTimeOffset ResetAt, long Used);
}
