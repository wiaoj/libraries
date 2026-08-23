using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Internal.Memory;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Extension methods for configuring in-memory storage on <see cref="IDistributedCounterBuilder"/>.
/// </summary>
public static class InMemoryDistributedCounterBuilderExtensions {
    /// <summary>
    /// Configures the distributed counter to use high-performance in-memory CAS storage.
    /// Ideal for local development, integration tests, and single-instance applications.
    /// </summary>
    /// <param name="builder">The distributed counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseInMemory(this IDistributedCounterBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, InMemoryCounterStorage>();

        return builder;
    }
}