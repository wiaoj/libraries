using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.DistributedCounter.Internal.Memory;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring in-memory storage on <see cref="IDistributedCounterBuilder"/> and <see cref="CounterConfiguration"/>.
/// </summary>
public static class InMemoryDistributedCounterBuilderExtensions {
    /// <summary>
    /// Configures the distributed counter engine to use high-performance in-memory CAS storage as the global default.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseInMemory(this IDistributedCounterBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, InMemoryCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures this specific counter or tag to use isolated in-memory storage.
    /// </summary>
    /// <param name="config">The counter configuration.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public static CounterConfiguration UseInMemory(this CounterConfiguration config) {
        Preca.ThrowIfNull(config);
        return config.UseStorage<InMemoryCounterStorage>();
    }
}