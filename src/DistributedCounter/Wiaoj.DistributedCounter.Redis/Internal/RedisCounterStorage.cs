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

        RedisKey[] keys = [key.Value];
        RedisValue[] values = [amount, (long)ttl.TotalMilliseconds];

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

        RedisKey[] keys = [key.ToRedisKey()];
        RedisValue[] values = [amount, limit, ttlMs];

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

        RedisKey[] keys = [key.ToRedisKey()];
        RedisValue[] values = [amount, minLimit, ttlMs];

        RedisResult result = await this.Db.ScriptEvaluateAsync(
            DistributedCounterRedisLuaScripts.DecrementIfGreaterThan,
            keys,
            values
        ).ConfigureAwait(false);

        return ParseLimitResult(result, minLimit, isDecrement: true);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryCompareExchangeAsync(
        CounterKey key,
        CounterValue expectedValue,
        CounterValue newValue,
        CounterExpiry expiry,
        CancellationToken cancellationToken) {

        long ttlMs = expiry.GetTtlMilliseconds();
        RedisKey[] keys = [key.ToRedisKey()];
        RedisValue[] values = [expectedValue.Value, newValue.Value, ttlMs];

        RedisResult result = await this.Db.ScriptEvaluateAsync(
            DistributedCounterRedisLuaScripts.CompareExchangeWithExpire,
            keys,
            values
        ).ConfigureAwait(false);

        if(result.IsNull) return false;

        RedisResult[] parts = (RedisResult[])result!;
        return (long)parts[0] == 1;
    }

    /// <summary>
    /// Translates the raw Lua script result ({isAllowed, currentValue, pttl}) into a <see cref="CounterLimitResult"/>.
    /// </summary>
    private static CounterLimitResult ParseLimitResult(RedisResult result, long limitOrMin, bool isDecrement = false) {
        if(result.IsNull) {
            return new CounterLimitResult(IsAllowed: false, CurrentValue: 0, Remaining: 0, Ttl: null);
        }

        RedisResult[] parts = (RedisResult[])result!;
        bool isAllowed = (long)parts[0] == 1;
        long val = (long)parts[1];
        TimeSpan? ttl = ParsePttl((long)parts[2]);

        if(!isAllowed) {
            return new CounterLimitResult(IsAllowed: false, CurrentValue: val, Remaining: 0, Ttl: ttl);
        }

        long remaining = isDecrement ? (val - limitOrMin) : (limitOrMin - val);
        return new CounterLimitResult(IsAllowed: true, CurrentValue: val, Remaining: remaining, Ttl: ttl);
    }

    private static TimeSpan? ParsePttl(long pttlMs) {
        return pttlMs >= 0 ? TimeSpan.FromMilliseconds(pttlMs) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken = default) {
        RedisValue val = await this.Db.StringGetAsync(key.Value).ConfigureAwait(false);
        return val.ToCounter();
    }

    /// <inheritdoc/>
    public async ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken = default) {
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
            destSpan[i] = (long)tasks[i].Result;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken = default) {
        return await this.Db.KeyTimeToLiveAsync(key.ToRedisKey()).ConfigureAwait(false);
    }
}