using StackExchange.Redis;
using System.Buffers;

namespace Wiaoj.DistributedCounter.Redis.Internal;

/// <summary>
/// Redis-backed <see cref="ICounterStorage"/> implementation. Limit-checked operations
/// (<see cref="TryIncrementAsync"/>, <see cref="TryDecrementAsync"/>) execute as single-round-trip
/// Lua scripts to keep the read-check-write sequence atomic under concurrent access.
/// </summary>
internal sealed class RedisCounterStorage : ICounterStorage {
    private readonly IConnectionMultiplexer _redis;
    private readonly int? _dbIndex = null;

    public RedisCounterStorage(IConnectionMultiplexer redis) {
        this._redis = redis;
    }

    private IDatabase Db => this._redis.GetDatabase(this._dbIndex ?? -1);

    /// <inheritdoc/>
    public async ValueTask<CounterValue> AtomicIncrementAsync(
        CounterKey key,
        long amount,
        CounterExpiry expiry,
        CancellationToken cancellationToken = default) {

        if(!expiry.Value.HasValue) {
            long result = await this.Db.StringIncrementAsync(key.Value, amount).ConfigureAwait(false);
            return new CounterValue(result);
        }

        TimeSpan ttl = expiry.Value.Value;

        // Script: IncrementWithExpire
        // KEYS[1]: Key
        // ARGV[1]: Amount
        // ARGV[2]: TTL (ms)
        RedisKey[] keys = new RedisKey[] { key.Value };
        RedisValue[] values = new RedisValue[] { amount, (long)ttl.TotalMilliseconds };

        RedisResult resultLua = await this.Db.ScriptEvaluateAsync(
            DistributedCounterRedisLuaScripts.IncrementWithExpire,
            keys,
            values
        ).ConfigureAwait(false);

        return new CounterValue((long)resultLua);
    }

    /// <inheritdoc/>
    public async ValueTask<CounterLimitResult> TryIncrementAsync(CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        long ttlMs = expiry.GetTtlMilliseconds();

        // Script: IncrementIfLessThan
        // KEYS[1]: Key
        // ARGV[1]: Amount
        // ARGV[2]: Limit
        // ARGV[3]: TTL — set only on the first successful increment of a window (current == 0
        // inside the script), which is what makes this a correct fixed-window primitive: the
        // window starts on first use and every subsequent increment shares the same TTL.
        RedisKey[] keys = new RedisKey[] { key.ToRedisKey() };
        RedisValue[] values = new RedisValue[] { amount, limit, ttlMs };

        RedisResult result = await this.Db.ScriptEvaluateAsync(
            DistributedCounterRedisLuaScripts.IncrementIfLessThan,
            keys,
            values
        ).ConfigureAwait(false);

        return ParseLimitResult(result, limit);
    }

    /// <inheritdoc/>
    public async ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        long ttlMs = expiry.GetTtlMilliseconds();

        // Script: DecrementIfGreaterThan
        // KEYS[1]: Key
        // ARGV[1]: Amount
        // ARGV[2]: MinLimit
        // ARGV[3]: TTL
        RedisKey[] keys = new RedisKey[] { key.ToRedisKey() };
        RedisValue[] values = new RedisValue[] { amount, minLimit, ttlMs };

        RedisResult result = await this.Db.ScriptEvaluateAsync(
            DistributedCounterRedisLuaScripts.DecrementIfGreaterThan,
            keys,
            values
        ).ConfigureAwait(false);

        return ParseLimitResult(result, minLimit, isDecrement: true);
    }

    /// <summary>
    /// Translates the raw Lua script result (a long, or <c>-1</c> to signal denial) into a
    /// <see cref="CounterLimitResult"/>.
    /// </summary>
    private static CounterLimitResult ParseLimitResult(RedisResult result, long limitOrMin, bool isDecrement = false) {
        if(result.IsNull) {
            return new CounterLimitResult(IsAllowed: false, CurrentValue: limitOrMin, Remaining: 0, Ttl: null);
        }

        RedisResult[] parts = (RedisResult[])result!;
        long val = (long)parts[0];
        TimeSpan? ttl = ParsePttl((long)parts[1]);

        if(val == -1) {
            return new CounterLimitResult(IsAllowed: false, CurrentValue: limitOrMin, Remaining: 0, Ttl: ttl);
        }

        long remaining = isDecrement ? (val - limitOrMin) : (limitOrMin - val);
        return new CounterLimitResult(IsAllowed: true, CurrentValue: val, Remaining: remaining, Ttl: ttl);
    }

    /// <summary>PTTL semantics: -2 = key missing, -1 = no expiry, >=0 = remaining ms.</summary>
    private static TimeSpan? ParsePttl(long pttlMs)
        => pttlMs >= 0 ? TimeSpan.FromMilliseconds(pttlMs) : null;

    /// <inheritdoc/>
    public async ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken = default) {
        RedisValue val = await this.Db.StringGetAsync(key.Value).ConfigureAwait(false);
        return val.ToCounter();
    }

    /// <inheritdoc/>
    public async ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys,
                                                                               CancellationToken cancellationToken = default) {
        RedisKey[] keyArray = [.. keys.Select(k => k.ToRedisKey())];
        if(keyArray.Length == 0) return new Dictionary<CounterKey, CounterValue>();

        RedisValue[] values = await this.Db.StringGetAsync(keyArray).ConfigureAwait(false);
        Dictionary<CounterKey, CounterValue> result = new(keyArray.Length);

        int index = 0;
        foreach(CounterKey k in keys) {
            RedisValue val = values[index];
            result[k] = val.ToCounter();
            index++;
        }
        return result;
    }

    /// <inheritdoc/>
    public async ValueTask GetManyAsync(
        ReadOnlyMemory<CounterKey> keys,
        Memory<CounterValue> destination,
        CancellationToken cancellationToken = default) {

        if(keys.IsEmpty) return;
        int count = keys.Length;
        RedisKey[] redisKeys = ArrayPool<RedisKey>.Shared.Rent(count);

        try {
            ReadOnlySpan<CounterKey> keysSpan = keys.Span;
            for(int i = 0; i < count; i++) {
                redisKeys[i] = keysSpan[i].ToRedisKey();
            }

            RedisKey[] actualKeys = [.. redisKeys.Take(count)];
            RedisValue[] redisValues = await this.Db.StringGetAsync(actualKeys).ConfigureAwait(false);

            Span<CounterValue> destSpan = destination.Span;
            for(int i = 0; i < count; i++) {
                RedisValue val = redisValues[i];
                destSpan[i] = val.ToCounter();
            }
        }
        finally {
            ArrayPool<RedisKey>.Shared.Return(redisKeys);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken = default) {
        await this.Db.KeyDeleteAsync(key.Value).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync(
        CounterKey key,
        CounterValue value,
        CounterExpiry expiry,
        CancellationToken cancellationToken = default) {

        await this.Db.StringSetAsync(
            key.Value,
            value.Value,
            expiry.Value,
            keepTtl: false).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken = default) {
        if(updates.IsEmpty) return;

        IBatch batch = this.Db.CreateBatch();
        ReadOnlySpan<CounterUpdate> span = updates.Span;
        Task<RedisResult>[] tasks = new Task<RedisResult>[span.Length];

        for(int i = 0; i < span.Length; i++) {
            ref readonly CounterUpdate update = ref span[i];

            long ttlMs = update.Expiry.GetTtlMilliseconds();

            // KEYS[1], ARGV[1]: Amount, ARGV[2]: TTL
            tasks[i] = batch.ScriptEvaluateAsync(
                DistributedCounterRedisLuaScripts.IncrementWithExpire,
                [update.Key.Value],
                [update.Amount, ttlMs]
            );
        }

        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        Span<long> destSpan = resultDestination.Span;
        for(int i = 0; i < tasks.Length; i++) {
            // Lua script result döner, long'a cast ediyoruz
            destSpan[i] = (long)tasks[i].Result;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken = default) {
        return await this.Db.KeyTimeToLiveAsync(key.ToRedisKey()).ConfigureAwait(false);
    }
}
