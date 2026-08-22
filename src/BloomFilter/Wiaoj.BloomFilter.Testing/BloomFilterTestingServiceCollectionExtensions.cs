using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Testing extension methods for registering fake Bloom Filters in <see cref="IServiceCollection"/>.
/// </summary>
public static class BloomFilterTestingServiceCollectionExtensions {
    /// <summary>
    /// Registers a fake singleton <see cref="IBloomFilter"/> under the specified name.
    /// </summary>
    public static IServiceCollection AddFakeBloomFilter(this IServiceCollection services, string filterName) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNullOrWhiteSpace(filterName);

        FakeBloomFilter fake = new(filterName);
        services.AddKeyedSingleton<IBloomFilter>(filterName, fake);
        services.TryAddSingleton<IBloomFilter>(fake);
        return services;
    }

    /// <summary>
    /// Registers a strongly-typed fake singleton <see cref="IBloomFilter{TTag}"/>.
    /// </summary>
    public static IServiceCollection AddFakeBloomFilter<TTag>(this IServiceCollection services) where TTag : notnull {
        Preca.ThrowIfNull(services);

        FakeBloomFilter<TTag> fake = new();
        services.AddSingleton<IBloomFilter<TTag>>(fake);
        services.TryAddSingleton<IBloomFilter>(fake);
        return services;
    }

    /// <summary>
    /// Registers the in-memory test storage for Bloom Filter persistence testing.
    /// </summary>
    public static IServiceCollection AddInMemoryBloomFilterStorage(this IServiceCollection services) {
        Preca.ThrowIfNull(services);
        services.RemoveAll<IBloomFilterStorage>();
        services.AddSingleton<IBloomFilterStorage, InMemoryBloomFilterStorage>();
        return services;
    }
}