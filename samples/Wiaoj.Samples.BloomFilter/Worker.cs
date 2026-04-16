using System.Diagnostics.CodeAnalysis;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Diagnostics;

namespace Wiaoj.Samples.BloomFilter;

public class BloomTestWorker : BackgroundService {
    private readonly IBloomFilterProvider _provider;

    public BloomTestWorker(IBloomFilterProvider provider) {
        _provider = provider;
    }
     
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // 1. Scalable Filtreyi Al
        var scalable = await _provider.GetAsync("TestFilter");

        // 2. Test Verisi Ekle
        for(int i = 0; i < 500_000; i++) {
            scalable.Add($"user_{i}");
        }

        // 3. Görsel Kontrol (AOT testi burada: Reflection çalışıyor mu?)
        string report = BloomFilterInspector.GetVisualRepresentation(scalable);
        Console.WriteLine(report);

        await Task.CompletedTask;
    }
}