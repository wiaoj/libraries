using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Seeder;

namespace Wiaoj.BloomFilter.Hosting;

internal sealed class BloomFilterSeedingService(
    IBloomFilterRegistry registry,
    IEnumerable<IAutoBloomFilterSeeder> seeders,
    ILogger<BloomFilterSeedingService> logger,
    IBloomFilterStorage? storage = null) : BackgroundService {

    private static readonly ConcurrentDictionary<FilterName, bool> s_seededFilters = new();

    internal static void ResetSeededState() => s_seededFilters.Clear();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        foreach(IPersistentBloomFilter filter in registry.GetAll()) {
            if(s_seededFilters.ContainsKey(filter.Name)) {
                continue;
            }

            if(storage != null) {
                (BloomFilterConfiguration? Config, Stream DataStream)? existingSnapshot = await storage.LoadStreamAsync(filter.Name, stoppingToken).ConfigureAwait(false);
                if(existingSnapshot.HasValue) {
                    await existingSnapshot.Value.DataStream.DisposeAsync().ConfigureAwait(false);
                    s_seededFilters.TryAdd(filter.Name, true);
                    continue;
                }
            }

            if(filter.GetPopCount() == 0) {
                IEnumerable<IAutoBloomFilterSeeder> filterSeeders = seeders.Where(s => s.FilterName.Value == filter.Name);
                bool anySeeded = false;
                foreach(IAutoBloomFilterSeeder? seeder in filterSeeders) {
                    try {
                        logger.LogInformation("Auto-seeding filter: {Name}", filter.Name);
                        await seeder.SeedAsync(filter, stoppingToken).ConfigureAwait(false);
                        anySeeded = true;
                    }
                    catch(Exception ex) {
                        logger.LogError(ex, "Seeding failed for {Name}", filter.Name);
                    }
                }

                if(anySeeded) {
                    try {
                        if(storage != null && !filter.IsDirty) {
                            using MemoryStream ms = new();
                            BloomFilterHeader.WriteHeader(ms, 0, filter.Configuration);
                            using PooledBitArray emptyBits = new(filter.Configuration.SizeInBits);
                            emptyBits.WriteToStream(ms);
                            ms.Position = 0;
                            await storage.SaveAsync(filter.Name, filter.Configuration, ms, stoppingToken).ConfigureAwait(false);
                        }
                        else {
                            await filter.SaveAsync(stoppingToken).ConfigureAwait(false);
                        }
                    }
                    catch(Exception ex) {
                        logger.LogError(ex, "Post-seeding save failed for {Name}", filter.Name);
                    }
                }

                s_seededFilters.TryAdd(filter.Name, true);
            }
            else {
                s_seededFilters.TryAdd(filter.Name, true);
            }
        }
    }
}