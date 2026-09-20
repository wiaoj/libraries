using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Idempotency;
#pragma warning restore IDE0130

/// <summary>
/// Registration for idempotency stores.
/// </summary>
public static class IdempotencyServiceCollectionExtensions {
    /// <summary>
    /// Registers the in-process <see cref="InMemoryIdempotencyStore"/> as the <see cref="IIdempotencyStore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// A claim recorded here lives only in this process: it is forgotten on restart and not shared with other replicas.
    /// That is the case idempotency exists for, so use a distributed store (<c>Wiaoj.Idempotency.Redis</c>) wherever more
    /// than one process, or a process that can restart, handles the same messages.
    /// </remarks>
    public static IServiceCollection AddInMemoryIdempotencyStore(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdempotencyStore>(static sp => new InMemoryIdempotencyStore(sp.GetRequiredService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IIdempotencyStore"/> built by the given factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">The factory that creates the store.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddIdempotencyStore(this IServiceCollection services, Func<IServiceProvider, IIdempotencyStore> factory) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(factory);

        services.RemoveAll<IIdempotencyStore>();
        services.AddSingleton(factory);

        return services;
    }
}
