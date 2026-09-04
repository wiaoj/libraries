using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.BloomFilter.Engine;

namespace Wiaoj.BloomFilter.Hosting;

internal sealed class BloomFilterAutoSaveService(
    IBloomFilterRegistry registry,
    TimeProvider timeProvider,
    IOptions<BloomFilterOptions> options,
    ILogger<BloomFilterAutoSaveService> logger) : BackgroundService {

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(options.Value.Lifecycle.AutoSaveInterval <= TimeSpan.Zero) return;

        using PeriodicTimer timer = new(options.Value.Lifecycle.AutoSaveInterval, timeProvider);

        try {
            while(await timer.WaitForNextTickAsync(stoppingToken)) {
                logger.LogAutoSaveTriggered();

                foreach(IPersistentBloomFilter filter in registry.GetAll()) {
                    if(filter.IsDirty) {
                        try { await filter.SaveAsync(stoppingToken); }
                        catch(Exception ex) { logger.LogAutoSaveFailed(ex, FilterName.Parse(filter.Name)); }
                    }
                }
            }
        }
        catch(OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Performing final save...");
        foreach(IPersistentBloomFilter filter in registry.GetAll()) {
            if(filter.IsDirty) {
                try { await filter.SaveAsync(CancellationToken.None); }
                catch(Exception ex) { logger.LogError(ex, "Final save failed for {Name}", filter.Name); }
            }
        }
        await base.StopAsync(cancellationToken);
    }
}