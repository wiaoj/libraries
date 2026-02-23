namespace Wiaoj.DistributedCounter.Redis.Internal;
internal static class RedisLuaScripts { 
    public const string IncrementWithExpire = @"
        local current = redis.call('INCRBY', KEYS[1], ARGV[1])
        if ARGV[2] and ARGV[2] ~= '' and ARGV[2] ~= '0' then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return current
    ";

    public const string IncrementWithExpireIfNew = @"
        local current = redis.call('INCRBY', KEYS[1], ARGV[1])
        if tonumber(current) == tonumber(ARGV[1]) then
            if ARGV[2] then
                redis.call('PEXPIRE', KEYS[1], ARGV[2])
            end
        end
        return current
    ";

    public const string IncrementIfLessThan = @"
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        local amount = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local new_val = current + amount

        if new_val <= limit then
            redis.call('INCRBY', KEYS[1], ARGV[1])
            if ARGV[3] and ARGV[3] ~= '0' then
                redis.call('PEXPIRE', KEYS[1], ARGV[3])
            end
            return new_val
        else
            return -1
        end
    ";

    public const string DecrementIfGreaterThan = @"
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        local amount = tonumber(ARGV[1])
        local min_limit = tonumber(ARGV[2])
        local new_val = current - amount

        if new_val >= min_limit then
            redis.call('DECRBY', KEYS[1], ARGV[1])
            if ARGV[3] and ARGV[3] ~= '0' then
                redis.call('PEXPIRE', KEYS[1], ARGV[3])
            end
            return new_val
        else
            return -1
        end
    ";
}