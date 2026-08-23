using System.Collections.Concurrent;
using Wiaoj.Extensions;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A leaky-bucket-as-queue <see cref="IRateLimitAlgorithm"/>: requests are admitted as long as the
/// scheduled backlog for a key doesn't exceed <c>capacity</c>, and <see cref="TryAcquireAsync"/>
/// actually <b>waits</b> — via <see cref="TimeProvider.Delay(TimeSpan, CancellationToken)"/> —
/// until the requests ahead of it have drained before returning <see cref="RateLimitDecision.Allowed"/>.
/// Only when the backlog itself is already full does a request get rejected outright, and that
/// rejection is immediate — no waiting to find out you were going to be denied anyway.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the traffic-shaping sibling of <see cref="LeakyBucketRateLimiter"/> and
/// <see cref="GcraRateLimiter"/>, not just another naming of the same decision procedure.</b> Those
/// two are meters: they decide instantly, and a request that doesn't fit right now is simply
/// denied with a <see cref="RateLimitDecision.RetryAfter"/> the caller is expected to act on
/// itself. This type instead smooths bursts into a steady release rate by holding admitted-but-not-
/// yet-their-turn requests inside the call — the same distinction nginx draws between
/// <c>limit_req burst=N nodelay;</c> (meter) and <c>limit_req burst=N;</c> (queue). Reach for this
/// when you want callers throttled to a steady output rate automatically; reach for the meters when
/// you want an instant yes/no and to own the backoff decision yourself.
/// </para>
/// <para>
/// <b>Admission math is GCRA underneath</b> — a per-key theoretical-arrival-time (<c>TAT</c>) is
/// advanced by <c>emissionInterval = period / capacity</c> for every unit of cost admitted, exactly
/// as <see cref="GcraRateLimiter"/> computes it. The difference is purely in what happens once a
/// request is deemed admissible: instead of returning immediately regardless of how far the TAT sits
/// in the future, this type computes how long until the backlog ahead of the new request has drained
/// (<c>baseline - now</c>) and awaits exactly that long before completing.
/// </para>
/// <para>
/// <b>Cost of holding the call open:</b> unlike every other algorithm in this package, a pending
/// <see cref="TryAcquireAsync"/> call here can genuinely take wall-clock time to complete — up to
/// nearly the full <c>period</c> for a request that lands at the back of a full backlog. That ties
/// up whatever resource is awaiting it (an ASP.NET Core request, a connection, a thread-pool
/// continuation) for the duration. This is an intentional trade-off of the shaping approach, not an
/// oversight — if that cost is unacceptable for a given call site, use <see cref="GcraRateLimiter"/>
/// or <see cref="LeakyBucketRateLimiter"/> instead and let the caller decide what "wait" means.
/// </para>
/// <para>
/// <b>Cancellation rolls back the reservation.</b> A request's slot in the backlog is reserved
/// atomically *before* it starts waiting (so concurrent callers see accurate backlog state
/// immediately). If the caller's <see cref="CancellationToken"/> fires while still waiting, the
/// reservation is compensated by subtracting the request's own contribution back out of the stored
/// TAT — otherwise a cancelled wait would permanently and silently consume capacity nobody ever
/// actually used. Because TAT accumulates additively, this compensation is exact except in the edge
/// case where the cancelled request was the one that revived an entirely idle queue (its baseline
/// was clamped to "now" rather than a prior TAT) — there it's a conservative approximation rather
/// than a bit-exact restoration. This mirrors the standard rollback-on-decrement pattern used by
/// production GCRA implementations.
/// </para>
/// <para>
/// <b>State shape:</b> single scalar TAT per key, same as <see cref="GcraRateLimiter"/> — see that
/// type's remarks for why this makes a distributed backend meaningfully simpler than
/// <see cref="TokenBucketRateLimiter"/>'s or <see cref="LeakyBucketRateLimiter"/>'s two-field state.
/// This implementation itself is still single-process (<see cref="ConcurrentDictionary{TKey,TValue}"/>-backed).
/// </para>
/// </remarks>
public sealed class LeakyBucketQueueRateLimiter : IRateLimitAlgorithm {
    private readonly int _capacity;
    private readonly TimeSpan _period;
    private readonly long _emissionIntervalTicks;
    private readonly long _maxBacklogTicks;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new leaky-bucket (queue/shaping) rate limiter.
    /// </summary>
    /// <param name="capacity">
    /// The maximum backlog a key can accumulate before further requests are rejected outright —
    /// equivalent to <see cref="GcraRateLimiter"/>'s burst limit. Must be greater than zero.
    /// </param>
    /// <param name="period">
    /// The time it takes a fully-backed-up key to drain back to empty at the steady release rate.
    /// Must be greater than <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <param name="timeProvider">
    /// The time provider driving both scheduling math and the actual wait. Pass a
    /// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> in tests — its
    /// <c>Advance(...)</c> drives pending <see cref="TimeProvider.Delay(TimeSpan, CancellationToken)"/>
    /// calls forward deterministically, with no real wall-clock waiting. Defaults to
    /// <see cref="TimeProvider.System"/> when omitted.
    /// </param>
    public LeakyBucketQueueRateLimiter(int capacity, TimeSpan period, TimeProvider? timeProvider = null) {
        if(capacity <= 0) {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }
        if(period <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than zero.");
        }

        this._capacity = capacity;
        this._period = period;
        this._emissionIntervalTicks = period.Ticks / capacity;
        this._maxBacklogTicks = this._emissionIntervalTicks * capacity;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Argument validation and the admission decision happen synchronously, before any awaiting —
    /// a caller with an invalid key or a backlog-full key finds out immediately, exactly like every
    /// other <see cref="IRateLimitAlgorithm"/> in this package. Only an admitted-but-not-yet-its-turn
    /// request actually suspends.
    /// </remarks>
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if(cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._capacity) {
            // No amount of draining ever lets this succeed — an empty backlog still can't absorb it.
            return ValueTask.FromResult(RateLimitDecision.Denied(this._period, remaining: this._capacity));
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        long incrementTicks = this._emissionIntervalTicks * cost;

        bool admitted = false;
        DateTimeOffset baseline = default;
        DateTimeOffset newTat = default;
        DateTimeOffset rejectAllowAt = default;

        this._state.AddOrUpdate(
            key,
            addValueFactory: _ => {
                // First-ever request for this key: no backlog yet, so it starts its own turn now.
                baseline = now;
                newTat = baseline.AddTicks(incrementTicks);
                admitted = true;
                return newTat;
            },
            updateValueFactory: (_, existingTat) => {
                baseline = existingTat > now ? existingTat : now;
                newTat = baseline.AddTicks(incrementTicks);
                long backlogTicks = (newTat - now).Ticks;

                if(backlogTicks > this._maxBacklogTicks) {
                    admitted = false;
                    rejectAllowAt = newTat.AddTicks(-this._maxBacklogTicks);
                    return existingTat; // rejected — leave the reserved backlog untouched
                }

                admitted = true;
                return newTat;
            });

        if(!admitted) {
            TimeSpan retryAfter = rejectAllowAt - now;
            if(retryAfter < TimeSpan.Zero) {
                retryAfter = TimeSpan.Zero;
            }

            return ValueTask.FromResult(RateLimitDecision.Denied(retryAfter, remaining: 0));
        }

        TimeSpan wait = baseline - now;
        if(wait <= TimeSpan.Zero) {
            // The backlog ahead of this request had already drained by "now" — no actual waiting
            // needed, resolve synchronously just like the meter-form algorithms do.
            return ValueTask.FromResult(RateLimitDecision.Allowed(ComputeRemaining(newTat, now)));
        }

        return new ValueTask<RateLimitDecision>(WaitForTurnAndCompleteAsync(key, incrementTicks, wait, newTat, cancellationToken));
    }

    private async Task<RateLimitDecision> WaitForTurnAndCompleteAsync(
        string key, long incrementTicks, TimeSpan wait, DateTimeOffset newTat, CancellationToken cancellationToken) {
        try {
            await this._timeProvider.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            // Best-effort rollback: subtract our own contribution back out so a cancelled wait
            // never permanently occupies capacity nobody actually used.
            this._state.AddOrUpdate(
                key,
                addValueFactory: static _ => default,
                updateValueFactory: (_, existingTat) => existingTat.AddTicks(-incrementTicks));
            throw;
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        return RateLimitDecision.Allowed(ComputeRemaining(newTat, now));
    }

    private long ComputeRemaining(DateTimeOffset newTat, DateTimeOffset now) {
        long backlogTicks = Math.Max(0, (newTat - now).Ticks);
        return Math.Max(0, (this._maxBacklogTicks - backlogTicks) / this._emissionIntervalTicks);
    }

    /// <summary>Clears all tracked state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }
}