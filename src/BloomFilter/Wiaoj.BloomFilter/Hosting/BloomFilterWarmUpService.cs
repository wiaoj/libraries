using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Wiaoj.BloomFilter.Hosting;
public class BloomFilterWarmUpService : BackgroundService {
    private readonly IBloomFilterProvider _provider;
    private readonly BloomFilterOptions _options;
    private readonly ILogger<BloomFilterWarmUpService> _logger;

    public BloomFilterWarmUpService(
        IBloomFilterProvider provider,
        IOptions<BloomFilterOptions> options,
        ILogger<BloomFilterWarmUpService> logger) {
        this._provider = provider;
        this._options = options.Value;
        this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(!this._options.Lifecycle.EnableWarmUp)
            return;

        this._logger.LogInformation("🔥 Warming up Bloom Filters...");
        Stopwatch sw = Stopwatch.StartNew();

        // Config'de kayıtlı tüm filtreleri paralel olarak tetikliyoruz.
        // GetAsync çağırmak, filtreyi Lazy yüklemeden çıkartıp RAM'e ve Shard'lara hazırlar.
        IEnumerable<Task> tasks = this._options.Filters.Keys.Select(async key => {
            try {
                FilterName name = FilterName.Parse(key);
                await this._provider.GetAsync(name);
            }
            catch(Exception ex) {
                this._logger.LogError(ex, "Failed to warm up '{Name}'", key);
            }
        });

        await Task.WhenAll(tasks);

        sw.Stop();
        this._logger.LogInformation("✅ All filters warmed up in {Elapsed}ms.", sw.ElapsedMilliseconds);
    }
}