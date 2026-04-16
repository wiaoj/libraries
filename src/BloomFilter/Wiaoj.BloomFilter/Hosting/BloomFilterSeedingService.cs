using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Seeder;

namespace Wiaoj.BloomFilter.Hosting; 
internal class BloomFilterSeedingService(
    IBloomFilterRegistry registry,
    IEnumerable<IAutoBloomFilterSeeder> seeders,
    ILogger<BloomFilterSeedingService> logger) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        foreach(IPersistentBloomFilter filter in registry.GetAll()) {
            // Eğer filtre boşsa (PopCount 0) doldur
            if(filter.GetPopCount() == 0) {
                IEnumerable<IAutoBloomFilterSeeder> filterSeeders = seeders.Where(s => s.FilterName.Value == filter.Name);
                foreach(IAutoBloomFilterSeeder? seeder in filterSeeders) {
                    try {
                        logger.LogInformation("Auto-seeding filter: {Name}", filter.Name);
                        await seeder.SeedAsync(filter, stoppingToken);
                    }
                    catch(Exception ex) {
                        logger.LogError(ex, "Seeding failed for {Name}", filter.Name);
                    }
                }
            }
        }
    }
}