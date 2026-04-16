using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Seeder;

namespace Wiaoj.BloomFilter.Seeding;

public class BloomFilterSeeder(IServiceProvider serviceProvider, ILogger<BloomFilterSeeder> logger) : IBloomFilterSeeder {

    public async Task SeedAsync<T>(
        FilterName filterName,
        IAsyncEnumerable<T> source,
        Func<T, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) {

        logger.LogSeedingStarted(filterName);

        var filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(filterName.Value);
        long count = 0;

        await foreach(T? item in source.WithCancellation(cancellationToken)) {
            filter.Add(serializer(item));
            count++;
            if(count % 100_000 == 0) logger.LogSeedingProgress(filterName, count);
        }

        logger.LogInformation("Seeding complete. Saving to storage...");
        await filter.SaveAsync(cancellationToken);
        logger.LogSeedingCompleted(filterName, count);
    }

    public Task SeedAsync(FilterName filterName, IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) =>
        SeedAsync(filterName, source, (str) => Encoding.UTF8.GetBytes(str), cancellationToken);

    public Task SeedAsync<TTag>(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) where TTag : notnull {
        var typedFilter = serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(FilterName.Parse(typedFilter.Name), source, cancellationToken);
    }

    public Task SeedAsync<TTag, TItem>(
        IAsyncEnumerable<TItem> source,
        Func<TItem, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) where TTag : notnull {
        var typedFilter = serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(FilterName.Parse(typedFilter.Name), source, serializer, cancellationToken);
    }
}