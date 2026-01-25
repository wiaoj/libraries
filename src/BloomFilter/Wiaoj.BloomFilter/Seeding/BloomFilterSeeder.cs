using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Seeder;

namespace Wiaoj.BloomFilter.Seeding;
/// <summary>
/// Default implementation for seeding Bloom Filters from external data sources.
/// </summary>
public class BloomFilterSeeder : IBloomFilterSeeder {
    private readonly IBloomFilterProvider _provider;
    private readonly ILogger<BloomFilterSeeder> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BloomFilterSeeder(IBloomFilterProvider provider, ILogger<BloomFilterSeeder> logger, IServiceProvider serviceProvider) {
        this._provider = provider;
        this._logger = logger;
        this._serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task SeedAsync<T>(
        FilterName filterName,
        IAsyncEnumerable<T> source,
        Func<T, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) {

        this._logger.LogSeedingStarted(filterName);

        IPersistentBloomFilter filter = await this._provider.GetAsync(filterName);
        long count = 0;

        await foreach(T? item in source.WithCancellation(cancellationToken)) {
            ReadOnlySpan<byte> bytes = serializer(item);
            filter.Add(bytes);

            count++;
            if(count % 100_000 == 0) {
                this._logger.LogSeedingProgress(filterName, count);
            }
        }

        if(filter is IPersistentBloomFilter persistent) {
            this._logger.LogInformation("Seeding complete. Saving to storage...");
            await persistent.SaveAsync(cancellationToken);
        }

        this._logger.LogSeedingCompleted(filterName, count);
    }

    /// <inheritdoc/>
    public Task SeedAsync(FilterName filterName, IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) {
        // Using Encoding.UTF8.GetBytes creates allocations per string.
        // For absolute max performance, user should provide a custom serializer that writes to a reused buffer.
        // However, this is the standard convenience method.
        return SeedAsync(filterName, source, (str) => Encoding.UTF8.GetBytes(str), cancellationToken);
    }

    /// <inheritdoc/>
    public Task SeedAsync<TTag>(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default)
      where TTag : notnull {
        IBloomFilter<TTag> typedFilter = this._serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(typedFilter.Name, source, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SeedAsync<TTag, TItem>(
        IAsyncEnumerable<TItem> source,
        Func<TItem, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) where TTag : notnull {
        IBloomFilter<TTag> typedFilter = this._serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(typedFilter.Name, source, serializer, cancellationToken);
    }
}