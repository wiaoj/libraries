using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.ObjectPool;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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

        // 1. Options binding from IConfiguration (appsettings.json)
        services.AddOptions<DistributedCounterOptions>()
            .ValidateOnStart();

        // 2. Base infrastructure
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICounterKeyBuilder, DefaultCounterKeyBuilder>();

        // 3. Engine Core & Factories
        services.TryAddSingleton<DistributedCounterFactory>();
        services.TryAddSingleton<IDistributedCounterFactory>(static sp => sp.GetRequiredService<DistributedCounterFactory>());
        services.TryAddSingleton<IDistributedCounterService, DistributedCounterService>();

        // 4. Open-generic typed counter wrapper (IDistributedCounter<T>)
        services.TryAddTransient(typeof(IDistributedCounter<>), typeof(TypedDistributedCounterWrapper<>));

        // 5. Zero-allocation Object Pool for batch operations
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