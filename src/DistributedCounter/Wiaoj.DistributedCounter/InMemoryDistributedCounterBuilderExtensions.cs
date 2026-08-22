using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Internal.Memory;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// Provides extension methods for configuring distributed counter storage on <see cref="IDistributedCounterBuilder"/>.
/// </summary>
public static class InMemoryDistributedCounterBuilderExtensions { 
    /// <summary>
    /// Configures the distributed counter to use In-Memory storage.
    /// Best for testing, development, or single-instance applications.
    /// NOT suitable for distributed environments (like Kubernetes with multiple replicas).
    /// </summary>
    public static IDistributedCounterBuilder UseInMemory(this IDistributedCounterBuilder builder) { 
        Preca.ThrowIfNull(builder);
        builder.Services.AddSingleton<ICounterStorage, InMemoryCounterStorage>();
        return builder;
    }

    /// <summary>
    /// Configures global <see cref="DistributedCounterOptions"/> for the builder.
    /// </summary>
    public static IDistributedCounterBuilder Configure(
        this IDistributedCounterBuilder builder,
        Action<DistributedCounterOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        configure(builder.Options);
        return builder;
    }
}