using StackExchange.Redis;
using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter.Redis;
public static class RedisExtensions {
    extension(RedisValue redisvalue) {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CounterValue ToCounter() {
           return redisvalue.IsNull ? CounterValue.Zero : new CounterValue((long)redisvalue);
        }
    }
     
    extension(CounterKey key) {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RedisKey ToRedisKey() {
            return key.Value;
        }
    }
}