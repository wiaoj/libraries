namespace Wiaoj.BloomFilter.Seeding;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Seeder;

/// <summary>
/// Default implementation of <see cref="IBloomFilterSeeder"/> for populating Bloom Filters from data sources.
/// </summary>
public class BloomFilterSeeder(IServiceProvider serviceProvider, ILogger<BloomFilterSeeder> logger) : IBloomFilterSeeder {

    /// <inheritdoc/>
    public async Task SeedAsync<T>(
        FilterName filterName,
        IAsyncEnumerable<T> source,
        Func<T, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) {

        logger.LogSeedingStarted(filterName);

        IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(filterName.Value);
        long count = 0; 
        long progressStep = Math.Max(1, filter.Configuration.ExpectedItems / 10);

        await foreach(T? item in source.WithCancellation(cancellationToken)) {
            if(item is not null) {
                filter.Add(serializer(item));
                if(++count % progressStep == 0) {
                    logger.LogSeedingProgress(filterName, count);
                }
            }
        }

        logger.LogInformation("Seeding complete. Saving to storage...");
        await filter.SaveAsync(cancellationToken);
        logger.LogSeedingCompleted(filterName, count);
    }

    /// <inheritdoc/>
    public async Task SeedAsync(FilterName filterName, IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) {
        logger.LogSeedingStarted(filterName);

        IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(filterName.Value);
        long count = 0;
        long progressStep = Math.Max(1, filter.Configuration.ExpectedItems / 10);

        await foreach(string? item in source.WithCancellation(cancellationToken)) {
            if(item is not null) {
                filter.Add(item.AsSpan());
                if(++count % progressStep == 0) {
                    logger.LogSeedingProgress(filterName, count);
                }
            }
        }

        logger.LogInformation("Seeding complete. Saving to storage...");
        await filter.SaveAsync(cancellationToken);
        logger.LogSeedingCompleted(filterName, count);
    }

    /// <inheritdoc/>
    public Task SeedAsync<TTag>(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) where TTag : notnull {
        IBloomFilter<TTag> typedFilter = serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(FilterName.Parse(typedFilter.Name), source, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SeedAsync<TTag, TItem>(
        IAsyncEnumerable<TItem> source,
        Func<TItem, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) where TTag : notnull {
        IBloomFilter<TTag> typedFilter = serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(FilterName.Parse(typedFilter.Name), source, serializer, cancellationToken);
    }
}