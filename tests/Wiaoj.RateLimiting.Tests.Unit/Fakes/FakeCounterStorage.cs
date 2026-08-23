using System.Collections.Concurrent;
using Wiaoj.DistributedCounter;

namespace Wiaoj.RateLimiting.Tests.Unit.Fakes;

/// <summary>
/// A minimal, deterministic <see cref="ICounterStorage"/> used to exercise the real algorithm
/// implementations (<see cref="FixedWindowRateLimiter"/>, <see cref="SlidingWindowRateLimiter"/>)
/// in isolation from the real Wiaoj.DistributedCounter storage implementations. Mirrors the
/// semantics of the Redis Lua scripts / <c>InMemoryCounterStorage</c>:
/// <list type="bullet">
/// <item><description><c>IncrementIfLessThan</c> (<see cref="TryIncrementAsync"/>): TTL is set only on the
/// first successful increment of a window, and the resulting PTTL is read back in the same
/// round-trip as the increment.</description></item>
/// <item><description><c>AtomicIncrementAsync</c>/<see cref="GetAsync"/>: unconditional increment (or, via a
/// negative amount, decrement) with sliding-window-style expiry reset semantics — needed by
/// <see cref="SlidingWindowRateLimiter"/>'s speculative-increment-then-rollback pattern.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Other members throw <see cref="NotSupportedException"/> deliberately — extend them if a future
/// test needs them, rather than stubbing silently wrong behavior.
/// </remarks>
public sealed class FakeCounterStorage : ICounterStorage {
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public FakeCounterStorage(TimeProvider? timeProvider = null) {
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    private static TimeSpan? ToTtl(DateTimeOffset? expiresAt, DateTimeOffset now) {
        return expiresAt is { } value ? value - now : null;
    }

    public ValueTask<CounterLimitResult> TryIncrementAsync(
        CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        CounterLimitResult decision = default;

        this._entries.AddOrUpdate(
            key.Value,
            addValueFactory: _ => {
                // First ever request for this key: current == 0, mirrors the Lua script's
                // "current == 0" branch — always allowed if amount <= limit, and starts the TTL.
                if(amount > limit) {
                    // Nothing was ever stored for this key, so there's no window to report a TTL for.
                    decision = new CounterLimitResult(IsAllowed: false, CurrentValue: 0, Remaining: 0, Ttl: null);
                    return new Entry(0, null);
                }
                DateTimeOffset? expiresAt = expiry.Value is { } ttl ? now + ttl : null;
                decision = new CounterLimitResult(IsAllowed: true, CurrentValue: amount, Remaining: limit - amount, Ttl: ToTtl(expiresAt, now));
                return new Entry(amount, expiresAt);
            },
            updateValueFactory: (_, existing) => {
                bool expired = existing.ExpiresAt is { } exp && now >= exp;
                long current = expired ? 0 : existing.Value;
                long newVal = current + amount;

                if(newVal > limit) {
                    // Denied — report the TTL of whatever window is currently live. If the window
                    // just expired (or never existed), there's nothing to report.
                    decision = new CounterLimitResult(IsAllowed: false, CurrentValue: current, Remaining: 0,
                        Ttl: expired ? null : ToTtl(existing.ExpiresAt, now));
                    // Denied attempts never mutate state — matches the Lua script, which only
                    // calls INCRBY/PEXPIRE inside the `if new_val <= limit` branch.
                    return expired ? new Entry(0, null) : existing;
                }

                // TTL is (re)established only when this is the first increment of a fresh window
                // (current == 0), exactly like `if current == 0 and ARGV[3] ... then PEXPIRE`.
                DateTimeOffset? expiresAt = current == 0 && expiry.Value is { } ttl
                    ? now + ttl
                    : existing.ExpiresAt;
                decision = new CounterLimitResult(IsAllowed: true, CurrentValue: newVal, Remaining: limit - newVal, Ttl: ToTtl(expiresAt, now));
                return new Entry(newVal, expiresAt);
            });

        return ValueTask.FromResult(decision);
    }

    /// <summary>
    /// Unconditional increment (negative <paramref name="amount"/> decrements). Resets the entry
    /// (fresh value, fresh TTL) if the previous one already expired — mirrors
    /// <c>InMemoryCounterStorage.AtomicIncrementAsync</c> exactly, since <see cref="SlidingWindowRateLimiter"/>
    /// relies on that reset behavior to age out old windows without an explicit cleanup pass.
    /// </summary>
    public ValueTask<CounterValue> AtomicIncrementAsync(CounterKey key, long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        Entry updated = this._entries.AddOrUpdate(
            key.Value,
            addValueFactory: _ => {
                DateTimeOffset? expiresAt = expiry.Value is { } ttl ? now + ttl : null;
                return new Entry(amount, expiresAt);
            },
            updateValueFactory: (_, existing) => {
                bool expired = existing.ExpiresAt is { } exp && now >= exp;
                if(expired) {
                    DateTimeOffset? expiresAt = expiry.Value is { } ttl ? now + ttl : null;
                    return new Entry(amount, expiresAt);
                }
                return existing with { Value = existing.Value + amount };
            });

        return ValueTask.FromResult(new CounterValue(updated.Value));
    }

    /// <inheritdoc/>
    public ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        if(this._entries.TryGetValue(key.Value, out Entry entry)) {
            bool expired = entry.ExpiresAt is { } exp && now >= exp;
            if(!expired) {
                return ValueTask.FromResult(new CounterValue(entry.Value));
            }
        }
        return ValueTask.FromResult(CounterValue.Zero);
    }

    public ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken) {
        if(!this._entries.TryGetValue(key.Value, out Entry entry) || entry.ExpiresAt is null) {
            return ValueTask.FromResult<TimeSpan?>(null);
        }

        TimeSpan remaining = entry.ExpiresAt.Value - this._timeProvider.GetUtcNow();
        return ValueTask.FromResult<TimeSpan?>(remaining > TimeSpan.Zero ? remaining : null);
    }

    /// <summary>Clears all tracked state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._entries.Clear();
    }

    // --- Members not needed by any algorithm today ---

    public ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeCounterStorage)} doesn't implement {nameof(TryDecrementAsync)} — extend it if a test needs it.");
    }

    public ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeCounterStorage)} doesn't implement {nameof(GetManyAsync)} — extend it if a test needs it.");
    }

    public ValueTask GetManyAsync(ReadOnlyMemory<CounterKey> keys, Memory<CounterValue> destination, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeCounterStorage)} doesn't implement {nameof(GetManyAsync)} — extend it if a test needs it.");
    }

    public ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeCounterStorage)} doesn't implement {nameof(DeleteAsync)} — extend it if a test needs it.");
    }

    public ValueTask SetAsync(CounterKey key, CounterValue value, CounterExpiry expiry, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeCounterStorage)} doesn't implement {nameof(SetAsync)} — extend it if a test needs it.");
    }

    public ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeCounterStorage)} doesn't implement {nameof(BatchIncrementAsync)} — extend it if a test needs it.");
    }

    private readonly record struct Entry(long Value, DateTimeOffset? ExpiresAt);
}