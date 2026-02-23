using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Redis;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
public static class RedisDistributedCounterBuilderExtensions {
    /// <summary>
    /// Configures the distributed counter to use Redis as the storage provider.
    /// Uses the provided connection string.
    /// </summary>
    public static IDistributedCounterBuilder UseRedis(this IDistributedCounterBuilder builder, string connectionString) {
        Preca.ThrowIfNullOrWhiteSpace(connectionString);

        // Builder içinden Services'a erişip kaydediyoruz.
        builder.Services.TryAddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(connectionString));
        builder.Services.TryAddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter to use Redis.
    /// Assumes IConnectionMultiplexer is already registered in DI.
    /// </summary>
    public static IDistributedCounterBuilder UseRedis(this IDistributedCounterBuilder builder) {
        builder.Services.TryAddSingleton<ICounterStorage, RedisCounterStorage>();
        return builder;
    }
}