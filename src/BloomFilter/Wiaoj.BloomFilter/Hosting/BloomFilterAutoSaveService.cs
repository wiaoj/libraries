using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Diagnostics;

namespace Wiaoj.BloomFilter.Hosting;
/// <summary>
/// A background service that periodically saves the state of all active Bloom Filters.
/// </summary>
public class BloomFilterAutoSaveService(
    IBloomFilterLifecycleManager lifecycleManager,
    TimeProvider timeProvider,
    IOptions<BloomFilterOptions> options,
    ILogger<BloomFilterAutoSaveService> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(options.Value.Lifecycle.AutoSaveInterval <= TimeSpan.Zero) return;

        using PeriodicTimer timer = new(options.Value.Lifecycle.AutoSaveInterval, timeProvider);

        try {
            while(await timer.WaitForNextTickAsync(stoppingToken)) {
                logger.LogAutoSaveTriggered();

                await lifecycleManager.SaveAllDirtyAsync(stoppingToken);
            }
        }
        catch(OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Performing final save...");
        try {
            await lifecycleManager.SaveAllDirtyAsync(CancellationToken.None);
        }
        catch(Exception ex) {
            logger.LogError(ex, "Final save failed.");
        }
        await base.StopAsync(cancellationToken);
    }
}