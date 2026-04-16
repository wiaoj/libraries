using System.Diagnostics;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Diagnostics;

namespace Wiaoj.Samples.BloomFilter;

public class BloomTestWorker : BackgroundService {
    private readonly IBloomFilter _filter;

    public BloomTestWorker([FromKeyedServices("TestFilter")] IBloomFilter filter) {
        this._filter = filter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // 1. Ufak bir bekleme (Yield) atıyoruz ki .NET Host rahatça ayağa kalkıp "Application started" loglarını basabilsin.
        await Task.Yield();

        try {
            Console.WriteLine("\n[WORKER] Başladı. 5.000.000 kayıt ekleniyor...");
            Stopwatch sw = Stopwatch.StartNew();

            //2.AĞIR İŞLEMİ(CPU - Bound) ARKA PLANA ATIYORUZ
           await Task.Run(() => {
               for(int i = 0; i < 5_000_000; i++) {
                   this._filter.Add($"user_{i}");

                   // HER 100 BİN KAYITTA BİR HABER VER
                   if(i % 100_000 == 0) {
                       Console.WriteLine($"Eklenen: {i} | Geçen Süre: {sw.ElapsedMilliseconds}ms");
                   }
               }
           }, stoppingToken);

            sw.Stop();
            Console.WriteLine($"[WORKER] Kayıtlar başarıyla eklendi! Süre: {sw.ElapsedMilliseconds} ms.\n");

            // 3. Görsel Kontrol
            string report = BloomFilterInspector.GetVisualRepresentation(this._filter);
            Console.WriteLine(report);
        }
        catch(Exception ex) {
            // Eğer RAM yetmezse veya başka bir şey patlarsa yutmasın, ekrana bassın.
            Console.WriteLine($"\n[WORKER HATASI] İşlem sırasında patladı: {ex}");
        }
    }
}