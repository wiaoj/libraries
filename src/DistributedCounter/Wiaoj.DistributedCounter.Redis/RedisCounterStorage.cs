using StackExchange.Redis;
using System.Buffers;
using Wiaoj.DistributedCounter.Redis.Internal;

namespace Wiaoj.DistributedCounter.Redis;

internal sealed class RedisCounterStorage : ICounterStorage {
    private readonly IConnectionMultiplexer _redis;
    private readonly int? _dbIndex = null;

    public RedisCounterStorage(IConnectionMultiplexer redis) {
        this._redis = redis;
    }

    private IDatabase Db => this._redis.GetDatabase(this._dbIndex ?? -1);

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

        // DÜZELTME: Anonymous object yerine explicit array kullanımı.
        // Script: IncrementWithExpire
        // KEYS[1]: Key
        // ARGV[1]: Amount
        // ARGV[2]: TTL (ms)
        RedisKey[] keys = new RedisKey[] { key.Value };
        RedisValue[] values = new RedisValue[] { amount, (long)ttl.TotalMilliseconds };

        RedisResult resultLua = await this.Db.ScriptEvaluateAsync(
            RedisLuaScripts.IncrementWithExpire,
            keys,
            values
        ).ConfigureAwait(false);

        return new CounterValue((long)resultLua);
    }

    public async ValueTask<CounterLimitResult> TryIncrementAsync(CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken ) {
        long ttlMs = expiry.GetTtlMilliseconds();

        // DÜZELTME: Parametre sırasını garantiye alıyoruz.
        // Script: IncrementIfLessThan
        // KEYS[1]: Key
        // ARGV[1]: Amount
        // ARGV[2]: Limit
        // ARGV[3]: TTL
        RedisKey[] keys = new RedisKey[] { key.ToRedisKey() };
        RedisValue[] values = new RedisValue[] { amount, limit, ttlMs };

        RedisResult result = await this.Db.ScriptEvaluateAsync(
            RedisLuaScripts.IncrementIfLessThan,
            keys,
            values
        ).ConfigureAwait(false);

        return ParseLimitResult(result, limit);
    }

    public async ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken ) {
        long ttlMs = expiry.GetTtlMilliseconds();

        // DÜZELTME: Parametre sırasını garantiye alıyoruz.
        // Script: DecrementIfGreaterThan
        // KEYS[1]: Key
        // ARGV[1]: Amount
        // ARGV[2]: MinLimit
        // ARGV[3]: TTL
        RedisKey[] keys = new RedisKey[] { key.ToRedisKey() };
        RedisValue[] values = new RedisValue[] { amount, minLimit, ttlMs };

        RedisResult result = await this.Db.ScriptEvaluateAsync(
            RedisLuaScripts.DecrementIfGreaterThan,
            keys,
            values
        ).ConfigureAwait(false);

        return ParseLimitResult(result, minLimit, isDecrement: true);
    }

    private static CounterLimitResult ParseLimitResult(RedisResult result, long limitOrMin, bool isDecrement = false) {
        // Lua'dan null dönerse (ki scriptlerde dönmüyor ama tedbir)
        if(result.IsNull) {
            return new CounterLimitResult(IsAllowed: false, CurrentValue: limitOrMin, Remaining: 0);
        }

        long val = (long)result;

        if(val == -1) {
            return new CounterLimitResult(IsAllowed: false, CurrentValue: limitOrMin, Remaining: 0);
        }

        long remaining = isDecrement ? (val - limitOrMin) : (limitOrMin - val);
        return new CounterLimitResult(IsAllowed: true, CurrentValue: val, Remaining: remaining);
    }

    public async ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken = default) {
        RedisValue val = await this.Db.StringGetAsync(key.Value).ConfigureAwait(false);
        return val.ToCounter();
    }

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

            // StackExchange.Redis için tam boyutta dizi gönderiyoruz
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

    public async ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken = default) {
        await this.Db.KeyDeleteAsync(key.Value).ConfigureAwait(false);
    }

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

    public async ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken = default) {
        if(updates.IsEmpty) return;

        IBatch batch = this.Db.CreateBatch();
        ReadOnlySpan<CounterUpdate> span = updates.Span;
        Task<RedisResult>[] tasks = new Task<RedisResult>[span.Length];

        for(int i = 0; i < span.Length; i++) {
            ref readonly var update = ref span[i];

            // Her update için TTL kontrolü yaparak Lua scriptini batch'e ekliyoruz
            long ttlMs = update.Expiry.GetTtlMilliseconds();

            // KEYS[1], ARGV[1]: Amount, ARGV[2]: TTL
            tasks[i] = batch.ScriptEvaluateAsync(
                RedisLuaScripts.IncrementWithExpire, // Mevcut scripti kullanıyoruz
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
}