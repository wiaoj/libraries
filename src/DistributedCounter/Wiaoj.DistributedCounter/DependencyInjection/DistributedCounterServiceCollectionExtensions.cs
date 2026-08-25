using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.ObjectPool;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for setting up distributed counter services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class DistributedCounterServiceCollectionExtensions {
    /// <summary>
    /// Adds core distributed counter infrastructure and returns a builder for fluent configuration.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <returns>An <see cref="IDistributedCounterBuilder"/> to configure storages and features.</returns>
    public static IDistributedCounterBuilder AddDistributedCounter(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.AddOptions<DistributedCounterOptions>()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICounterKeyBuilder, DefaultCounterKeyBuilder>();

        services.TryAddSingleton<DistributedCounterFactory>(static sp => new DistributedCounterFactory(
            sp.GetRequiredService<ICounterStorage>(),
            sp.GetRequiredService<ICounterKeyBuilder>(),
            sp.GetRequiredService<IOptions<DistributedCounterOptions>>(),
            sp));

        services.TryAddSingleton<IDistributedCounterFactory>(static sp => sp.GetRequiredService<DistributedCounterFactory>());

        services.TryAddSingleton<IDistributedCounterService>(static sp => new DistributedCounterService(
            sp.GetRequiredService<ICounterStorage>(),
            sp.GetRequiredService<ICounterKeyBuilder>(),
            sp.GetRequiredService<IDistributedCounterFactory>(),
            sp.GetRequiredService<IOptions<DistributedCounterOptions>>(),
            sp.GetRequiredService<IObjectPool<Dictionary<string, CounterValue>>>(),
            sp));

        services.TryAddTransient(typeof(IDistributedCounter<>), typeof(TypedDistributedCounterWrapper<>));

        services.AddObjectPool<Dictionary<string, CounterValue>>(
            factory: static () => new Dictionary<string, CounterValue>(StringComparer.Ordinal),
            resetter: static dict => {
                dict.Clear();
                return true;
            },
            configure: static opt => {
                opt.MaximumRetained = 1024;
                opt.AccessMode = PoolAccessMode.FIFO;
            }
        );

        return new DistributedCounterBuilder(services);
    }

    /// <summary>
    /// Adds distributed counter infrastructure and configures it using a builder delegate.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configure">The builder action.</param>
    /// <returns>The original <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddDistributedCounter(
        this IServiceCollection services,
        Action<IDistributedCounterBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        IDistributedCounterBuilder builder = services.AddDistributedCounter();
        configure(builder);

        return services;
    }
}