using System.Collections.Concurrent;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A <see href="https://en.wikipedia.org/wiki/Generic_cell_rate_algorithm">Generic Cell Rate
/// Algorithm</see> (GCRA) <see cref="IRateLimitAlgorithm"/>. GCRA is mathematically equivalent to
/// <see cref="TokenBucketRateLimiter"/> — same burst-then-steady-rate behavior, same numbers for
/// the same inputs — but expressed differently: instead of tracking "how many tokens are
/// currently in the bucket", it tracks a single value per key, the <b>TAT</b> (theoretical arrival
/// time) — the point in time at which the bucket would next be completely empty, projected
/// forward as requests are admitted. A request is allowed only if <c>now</c> is far enough back
/// from that projection that the configured burst tolerance still covers it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists alongside <see cref="TokenBucketRateLimiter"/>:</b> the token-bucket
/// implementation's own remarks call out its core limitation — token count and last-refill
/// timestamp are two fields that must be updated atomically together, which a plain
/// <see cref="DistributedCounter.IDistributedCounter"/> (a single <c>long</c> + TTL) can't express,
/// pushing a distributed implementation out to future work needing a two-field Lua script. GCRA
/// sidesteps that entirely: TAT is <i>one</i> value, so a distributed backend only needs the same
/// single-key atomic read-modify-write shape <see cref="FixedWindowRateLimiter"/> already uses via
/// <see cref="DistributedCounter.IDistributedCounter"/> — just storing a timestamp instead of a
/// count. That makes GCRA the more natural candidate to become the actual distributed,
/// burst-tolerant algorithm; this class is the in-process reference behavior a Redis-backed
/// sibling should match.
/// </para>
/// <para>
/// <b>Parameters map directly onto token bucket's:</b> <c>limit</c> is the maximum burst size
/// (equivalent to token-bucket <c>capacity</c>), <c>period</c> is the time to fully "drain" that
/// burst back down to zero debt at the steady rate (equivalent to token-bucket <c>window</c>).
/// Internally, <c>emissionInterval = period / limit</c> is the time cost of a single unit, and
/// <c>burstTolerance = emissionInterval * limit</c> (by construction, exactly <c>period</c> up to
/// integer-tick rounding) is how far into the future the TAT is allowed to run ahead of <c>now</c>
/// before a request is refused.
/// </para>
/// <para>
/// <b>State shape:</b> like <see cref="TokenBucketRateLimiter"/>, this implementation stores
/// per-key state in a <see cref="ConcurrentDictionary{TKey,TValue}"/> updated via <c>AddOrUpdate</c>'s
/// CAS loop — atomic and correct for a single process, but <b>not distributed</b>: multiple
/// instances each get their own TAT per key.
/// </para>
/// <para>
/// <b>Denial doesn't move TAT:</b> only admitted requests advance the projection. A denied request
/// leaves the stored TAT exactly as it was, so it never steals capacity a later, smaller request
/// could have used — the same non-mutation-on-denial guarantee every other algorithm in this
/// package provides.
/// </para>
/// </remarks>
public sealed class GcraRateLimiter : IRateLimitAlgorithm {
    private readonly int _limit;
    private readonly TimeSpan _period;
    private readonly long _emissionIntervalTicks;
    private readonly long _burstToleranceTicks;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new GCRA rate limiter.
    /// </summary>
    /// <param name="limit">
    /// The maximum burst size a fully-idle key can absorb instantly — equivalent to token-bucket
    /// <c>capacity</c>. Must be greater than zero.
    /// </param>
    /// <param name="period">
    /// The time it takes a fully-drained key to earn back its full burst allowance at the steady
    /// rate — equivalent to token-bucket <c>window</c>. Must be greater than <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <param name="timeProvider">
    /// The time provider driving TAT calculations. Pass a
    /// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> in tests; defaults to
    /// <see cref="TimeProvider.System"/> when omitted.
    /// </param>
    public GcraRateLimiter(int limit, TimeSpan period, TimeProvider? timeProvider = null) {
        if(limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }
        if(period <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than zero.");
        }

        this._limit = limit;
        this._period = period;
        // Integer-tick division (rather than double seconds, as TokenBucketRateLimiter uses) keeps
        // the emission interval and burst tolerance exactly consistent with each other by
        // construction (burstTolerance == emissionInterval * limit), with no floating-point drift
        // that could make a request denied/allowed by a rounding hair at the exact boundary.
        this._emissionIntervalTicks = period.Ticks / limit;
        this._burstToleranceTicks = this._emissionIntervalTicks * limit;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if(cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._limit) {
            // No amount of waiting ever lets this succeed — a fully-idle key still only tolerates
            // a burst of `limit`.
            return ValueTask.FromResult(RateLimitDecision.Denied(this._period, remaining: this._limit));
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        long incrementTicks = this._emissionIntervalTicks * cost;

        bool allowed = false;
        DateTimeOffset newTat = default;
        DateTimeOffset allowAt = default;

        this._state.AddOrUpdate(
            key,
            addValueFactory: _ => {
                // First-ever request for this key: no debt yet, so the projection starts from now.
                newTat = now.AddTicks(incrementTicks);
                allowAt = newTat.AddTicks(-this._burstToleranceTicks);
                allowed = now >= allowAt;
                return allowed ? newTat : now;
            },
            updateValueFactory: (_, existingTat) => {
                DateTimeOffset baseline = existingTat > now ? existingTat : now;
                newTat = baseline.AddTicks(incrementTicks);
                allowAt = newTat.AddTicks(-this._burstToleranceTicks);
                allowed = now >= allowAt;
                return allowed ? newTat : existingTat; // denied — leave the stored projection untouched
            });

        if(!allowed) {
            TimeSpan retryAfter = allowAt - now;
            if(retryAfter < TimeSpan.Zero) {
                retryAfter = TimeSpan.Zero;
            }

            return ValueTask.FromResult(RateLimitDecision.Denied(retryAfter, remaining: 0));
        }

        long debtTicks = (newTat - now).Ticks;
        long remaining = Math.Max(0, (this._burstToleranceTicks - debtTicks) / this._emissionIntervalTicks);

        return ValueTask.FromResult(RateLimitDecision.Allowed(remaining));
    }

    /// <summary>Clears all tracked state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }
}