namespace Wiaoj.DistributedCounter.Redis.Internal;

internal static class DistributedCounterRedisLuaScripts {
    public const string IncrementWithExpire = @"
        local current = redis.call('INCRBY', KEYS[1], ARGV[1])
        local ttl = tonumber(ARGV[2] or '0')
        if ttl > 0 then
            redis.call('PEXPIRE', KEYS[1], ttl)
        end
        return current
    ";

    public const string IncrementIfLessThan = @"
        local raw = redis.call('GET', KEYS[1])
        local is_new = (raw == false) or (raw == nil)
        local current = is_new and 0 or tonumber(raw)
        local amount = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local ttl = tonumber(ARGV[3] or '0')
        local new_val = current + amount

        if new_val <= limit then
            redis.call('INCRBY', KEYS[1], amount)
            if is_new and ttl > 0 then
                redis.call('PEXPIRE', KEYS[1], ttl)
            end
            return {1, new_val, redis.call('PTTL', KEYS[1])}
        else
            return {0, current, redis.call('PTTL', KEYS[1])}
        end
    ";

    public const string DecrementIfGreaterThan = @"
        local raw = redis.call('GET', KEYS[1])
        local is_new = (raw == false) or (raw == nil)
        local current = is_new and 0 or tonumber(raw)
        local amount = tonumber(ARGV[1])
        local min_limit = tonumber(ARGV[2])
        local ttl = tonumber(ARGV[3] or '0')
        local new_val = current - amount

        if new_val >= min_limit then
            redis.call('DECRBY', KEYS[1], amount)
            if is_new and ttl > 0 then
                redis.call('PEXPIRE', KEYS[1], ttl)
            end
            return {1, new_val, redis.call('PTTL', KEYS[1])}
        else
            return {0, current, redis.call('PTTL', KEYS[1])}
        end
    ";

    public const string CompareExchangeWithExpire = @"
        local raw = redis.call('GET', KEYS[1])
        local is_new = (raw == false) or (raw == nil)
        local current = is_new and 0 or tonumber(raw)
        local expected = tonumber(ARGV[1])
        local new_val = tonumber(ARGV[2])
        local ttl = tonumber(ARGV[3])
        if ttl == nil then ttl = 0 end

        if current == expected then
            redis.call('SET', KEYS[1], new_val)
            if ttl > 0 then
                redis.call('PEXPIRE', KEYS[1], ttl)
            end
            return {1, new_val}
        else
            return {0, current}
        end
    ";
}