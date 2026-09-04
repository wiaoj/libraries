
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Seeder;

namespace Wiaoj.BloomFilter.Seeding;
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

        long startingTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = BloomFilterDiagnostics.ActivitySource.StartActivity(BloomFilterDiagnostics.ActivitySeeding);
        activity?.SetTag(BloomFilterDiagnostics.TagFilterName, filterName.Value);

        logger.LogSeedingStarted(filterName);

        IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(filterName.Value);
        long count = 0;
        long progressStep = Math.Max(1, filter.Configuration.ExpectedItems / 10);
        try {
            await foreach(T? item in source.WithCancellation(cancellationToken)) {
                if(item is not null) {
                    filter.Add(serializer(item));
                    if(++count % progressStep == 0) {
                        logger.LogSeedingProgress(filterName, count);
                    }
                }
            }

            await filter.SaveAsync(cancellationToken);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startingTimestamp);
            BloomFilterDiagnostics.SeedingDuration.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, filterName.Value));

            activity?.SetTag("bloomfilter.items_seeded", count);
            logger.LogSeedingCompleted(filterName, count);
        }
        catch(Exception ex) {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SeedAsync(FilterName filterName, IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) {

        long startingTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = BloomFilterDiagnostics.ActivitySource.StartActivity(BloomFilterDiagnostics.ActivitySeeding);
        activity?.SetTag(BloomFilterDiagnostics.TagFilterName, filterName.Value);

        logger.LogSeedingStarted(filterName);

        IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(filterName.Value);
        long count = 0;
        long progressStep = Math.Max(1, filter.Configuration.ExpectedItems / 10);

        try {
            await foreach(string? item in source.WithCancellation(cancellationToken)) {
                if(item is not null) {
                    filter.Add(item.AsSpan());
                    if(++count % progressStep == 0) {
                        logger.LogSeedingProgress(filterName, count);
                    }
                }
            }

            await filter.SaveAsync(cancellationToken);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startingTimestamp);
            BloomFilterDiagnostics.SeedingDuration.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(BloomFilterDiagnostics.TagFilterName, filterName.Value));

            activity?.SetTag("bloomfilter.items_seeded", count);
            logger.LogSeedingCompleted(filterName, count);
        }
        catch(Exception ex) {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task SeedAsync<TTag>(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default) where TTag : notnull {
        IBloomFilter<TTag> typedFilter = serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(typedFilter.Name, source, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SeedAsync<TTag, TItem>(
        IAsyncEnumerable<TItem> source,
        Func<TItem, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default) where TTag : notnull {
        IBloomFilter<TTag> typedFilter = serviceProvider.GetRequiredService<IBloomFilter<TTag>>();
        return SeedAsync(typedFilter.Name, source, serializer, cancellationToken);
    }
}