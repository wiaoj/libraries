using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.Hosting;

internal class BloomFilterWarmUpService(
    IServiceProvider serviceProvider,
    IOptions<BloomFilterOptions> options,
    ILogger<BloomFilterWarmUpService> logger) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(!options.Value.Lifecycle.EnableWarmUp) return;

        logger.LogInformation("🔥 Warming up Bloom Filters...");
        Stopwatch sw = Stopwatch.StartNew();

        var tasks = options.Value.Filters.Keys.Select(async key => {
            try {
                var filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(key);
                if(filter is LazyBloomFilterProxy proxy) {
                    await proxy.EnsureInitializedAsync(stoppingToken);
                }
            }
            catch(Exception ex) {
                logger.LogError(ex, "Failed to warm up '{Name}'", key);
            }
        });

        await Task.WhenAll(tasks);

        sw.Stop();
        logger.LogInformation("✅ All filters warmed up in {Elapsed}ms.", sw.ElapsedMilliseconds);
    }
}