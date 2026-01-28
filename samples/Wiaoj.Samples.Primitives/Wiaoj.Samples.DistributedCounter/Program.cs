using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Wiaoj.DistributedCounter;
using Wiaoj.Primitives;

// Console Ayarları
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "Wiaoj Distributed Counter - Stress Test";

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// --- 1. LOGGING (Sadece kritik hataları görelim, ekranı kirletmesin) ---
builder.Logging.ClearProviders()
    .AddSimpleConsole(o => {
        o.TimestampFormat = "[HH:mm:ss] ";
        o.SingleLine = true;
    })
    .SetMinimumLevel(LogLevel.Warning);

// --- 2. KÜTÜPHANE KURULUMU ---
builder.Services.AddDistributedCounter(cfg => {
    cfg.Configure(options => {
        options.GlobalKeyPrefix = "app:stress_test:";
        options.AutoFlushInterval = TimeSpan.FromSeconds(3); // 3 sn'de bir oto-flush
        options.DefaultStrategy = CounterStrategy.Buffered;  // Varsayılan: Buffered
    });

    // Redis bağlantısı (Docker/Localhost varsayılanı)
    cfg.UseRedis("localhost:6379");
});

// --- 3. ORCHESTRATOR SERVİSİ ---
builder.Services.AddHostedService<MultiWorkerOrchestrator>();

IHost host = builder.Build();

// Uygulamayı başlat
try {
    await host.RunAsync();
}
catch(OperationCanceledException) {
    Console.WriteLine("🛑 Test sonlandırıldı.");
}

// -------------------------------------------------------------------------
// DOMAIN TYPES
// -------------------------------------------------------------------------

// Sayaç Grubu (Tag)
public class UserVisits { }

public enum WorkerType {
    SteadyIncrementer, // Düzenli küçük artışlar
    BurstSpiker,       // Arada bir devasa artışlar
    QuotaLimiter,      // Limite takılmaya çalışan (TryIncrement)
    Decreaser          // Arada azaltan (Stok iadesi gibi)
}

public class WorkerStats {
    public string Name { get; set; } = string.Empty;
    public WorkerType Type { get; set; }
    public long TotalContribution;
    public long SuccessOps;
    public long FailedOps;
    public string LastAction { get; set; } = "-";
}

// -------------------------------------------------------------------------
// ORCHESTRATOR (TEST SENARYOSU)
// -------------------------------------------------------------------------

public class MultiWorkerOrchestrator(
    IDistributedCounter<UserVisits> counter,
    IDistributedCounterService counterService,
    ILogger<MultiWorkerOrchestrator> logger)
    : BackgroundService {

    // Test Ayarları
    private const int WORKER_COUNT = 5;
    private const long GLOBAL_LIMIT = 5_000_000; // Quota worker için limit

    private readonly ConcurrentDictionary<int, WorkerStats> _workerRegistry = new();
    private readonly Stopwatch _stopwatch = new();
    private long _initialRedisValue = 0;

    // Renkler
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Cyan = "\x1b[36m";
    private const string Green = "\x1b[32m";
    private const string Yellow = "\x1b[33m";
    private const string Red = "\x1b[31m";
    private const string Magenta = "\x1b[35m";
    private const string Blue = "\x1b[34m";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Console.WriteLine($"{Yellow}Bağlantı kontrol ediliyor...{Reset}");

        // 1. Başlangıç Değerini Al (Global Key: app:stress_test:UserVisits)
        try {
            var initialVal = await counter.GetValueAsync(stoppingToken);
            _initialRedisValue = initialVal.Value;
        }
        catch(Exception ex) {
            Console.WriteLine($"{Red}Redis Bağlantı Hatası! Docker ayakta mı?{Reset}");
            logger.LogError(ex, "Startup error");
            return;
        }

        _stopwatch.Start();

        // 2. Worker'ları Başlat
        var tasks = new List<Task>();
        for(int i = 0; i < WORKER_COUNT; i++) {
            int id = i;
            // Farklı roller dağıt
            WorkerType type = (id % 4) switch {
                0 => WorkerType.SteadyIncrementer,
                1 => WorkerType.BurstSpiker,
                2 => WorkerType.QuotaLimiter,
                3 => WorkerType.Decreaser,
                _ => WorkerType.SteadyIncrementer
            };

            _workerRegistry[id] = new WorkerStats {
                Name = $"Worker-{id:00}",
                Type = type
            };

            tasks.Add(RunWorkerLogicAsync(id, type, stoppingToken));
        }

        // 3. Klavye Dinleyici (Arka planda)
        tasks.Add(InputListenerAsync(stoppingToken));

        // 4. UI Monitor (Ana Thread bu döngüde kalacak)
        await RunDashboardLoopAsync(stoppingToken);

        await Task.WhenAll(tasks);
    }

    private async Task RunWorkerLogicAsync(int id, WorkerType type, CancellationToken ct) {
        var stats = _workerRegistry[id];
        var rnd = Random.Shared;

        while(!ct.IsCancellationRequested) {
            try {
                long amount = 0;
                bool success = true;

                switch(type) {
                    case WorkerType.SteadyIncrementer:
                    // Sürekli küçük artışlar (1-10 arası)
                    amount = rnd.Next(1, 10);
                    // Extension method kullanımı: (amount, ct) -> Expiry infinite
                    await counter.IncrementAsync(amount, ct);
                    stats.LastAction = $"Inc({amount})";
                    break;

                    case WorkerType.BurstSpiker:
                    // %5 ihtimalle büyük artış, genelde bekleme
                    if(rnd.NextDouble() > 0.95) {
                        amount = rnd.Next(500, 2000);
                        await counter.IncrementAsync(amount, ct);
                        stats.LastAction = $"{Red}BURST({amount}){Reset}";
                        // Burst sonrası biraz dinlen
                        await Task.Delay(500, ct);
                    }
                    else {
                        await Task.Delay(50, ct);
                        continue; // İstatistik güncelleme
                    }
                    break;

                    case WorkerType.QuotaLimiter:
                    // Limite kadar artırmaya çalış (TryIncrement Testi)
                    amount = rnd.Next(100, 500);
                    // Extension: (amount, limit, expiry, ct) -> Expiry infinite
                    // ÖNEMLİ: Yeni interface sıralaması (Amount, Limit)
                    var result = await counter.TryIncrementAsync(amount, GLOBAL_LIMIT, CounterExpiry.Infinite, ct);

                    if(result.IsAllowed) {
                        stats.LastAction = $"TryInc({amount}) OK";
                    }
                    else {
                        success = false;
                        amount = 0; // Katkı sağlanamadı
                        stats.LastAction = $"{Red}LIMIT HIT!{Reset}";
                        stats.FailedOps++;
                    }
                    break;

                    case WorkerType.Decreaser:
                    // %20 ihtimalle azaltma yapar
                    amount = rnd.Next(1, 50);
                    if(rnd.NextDouble() > 0.8) {
                        // Decrement Extension
                        await counter.DecrementAsync(amount, ct);
                        amount = -amount; // Toplam katkı negatif
                        stats.LastAction = $"{Blue}Dec({Math.Abs(amount)}){Reset}";
                    }
                    else {
                        // Azaltıcı da bazen artırır ki dengelensin
                        await counter.IncrementAsync(amount, ct);
                        stats.LastAction = $"Inc({amount})";
                    }
                    break;
                }

                if(success) {
                    Interlocked.Add(ref stats.TotalContribution, amount);
                    Interlocked.Increment(ref stats.SuccessOps);
                }

                // Rastgele gecikme (Gerçekçilik için)
                await Task.Delay(rnd.Next(10, 100), ct);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                stats.LastAction = $"{Red}ERR: {ex.GetType().Name}{Reset}";
            }
        }
    }

    private async Task RunDashboardLoopAsync(CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            try {
                // Ekranı her 500ms'de bir güncelle
                RenderDashboard(ct);
                await Task.Delay(500, ct);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                logger.LogError(ex, "Dashboard Error");
                await Task.Delay(2000, ct);
            }
        }
    }

    private void RenderDashboard(CancellationToken ct) {
        var currentRedisValTask = counter.GetValueAsync(ct);
        if(!currentRedisValTask.IsCompleted) return;
        long currentRedisVal = currentRedisValTask.Result.Value;

        double elapsedSec = _stopwatch.Elapsed.TotalSeconds;
        long localTotalContribution = _workerRegistry.Values.Sum(x => x.TotalContribution);
        long localTotalOps = _workerRegistry.Values.Sum(x => x.SuccessOps + x.FailedOps);

        long totalGrowthInRedis = currentRedisVal - _initialRedisValue;

        // Hesaplama: (Benim RAM'de tuttuklarım + Gönderdiklerim) - (Redis'teki Artış)
        // Sonuç Pozitifse (+): RAM'de flush bekleyen veri var (Henüz gitmemiş).
        // Sonuç Negatifse (-): Başka biri (diğer konsollar) Redis'i şişirmiş.
        long diff = localTotalContribution - totalGrowthInRedis;

        Console.Clear();
        Console.WriteLine($"{Bold}{Magenta}╔════════════════════════════════════════════════════════════════════╗{Reset}");
        Console.WriteLine($"{Bold}{Magenta}║   WIAOJ DISTRIBUTED COUNTER - REAL-TIME ORCHESTRATOR               ║{Reset}");
        Console.WriteLine($"{Bold}{Magenta}╚════════════════════════════════════════════════════════════════════╝{Reset}");

        Console.WriteLine($" {Cyan}Strategy:{Reset} {counter.Strategy} | {Cyan}Key:{Reset} {counter.Key}");
        Console.WriteLine($" {Yellow}Controls:{Reset} [F]lush Force | [R]eset Global | [Q]uit");
        Console.WriteLine(new string('-', 70));

        // GLOBAL STATS
        Console.WriteLine($"{Bold}📊 GLOBAL STATUS{Reset}");
        Console.WriteLine($"  🌍 {Green}Redis Live Value :{Reset} {currentRedisVal,15:N0}");
        Console.WriteLine($"  💾 {Blue}Local Contribution :{Reset} {localTotalContribution,15:N0} (Bu pencerenin katkısı)");

        // --- MANTIK DÜZELTMESİ BURADA ---
        if(diff > 0) {
            // Pozitif fark: Bizim RAM'de verimiz var, henüz gitmemiş.
            Console.WriteLine($"  ⏳ {Yellow}Pending / Buffer :{Reset} {diff,15:N0} (Flush Bekleniyor)");
        }
        else if(diff < 0) {
            // Negatif fark: Bizden daha fazla artış olmuş (Diğer konsollar çalışıyor).
            long externalWrites = Math.Abs(diff);
            Console.WriteLine($"  🚀 {Magenta}External Writes  :{Reset} {externalWrites,15:N0} (Diğer App'lerden Gelen)");
        }
        else {
            Console.WriteLine($"  ✅ {Green}Synchronized     :{Reset} {0,15}");
        }

        double tps = localTotalOps / elapsedSec;
        Console.WriteLine($"  ⚡ {Cyan}Throughput (TPS) :{Reset} {tps,15:F1} ops/sec");

        Console.WriteLine(new string('-', 70));

        // WORKER TABLE
        Console.WriteLine($"{Bold}{"WORKER",-12} | {"TYPE",-16} | {"OPS",-8} | {"CONTRIB.",-12} | {"ACTION",-20}{Reset}");
        Console.WriteLine(new string('─', 78));

        foreach(var worker in _workerRegistry.Values.OrderBy(x => x.Name)) {
            string typeColor = worker.Type switch {
                WorkerType.SteadyIncrementer => Green,
                WorkerType.BurstSpiker => Red,
                WorkerType.QuotaLimiter => Yellow,
                WorkerType.Decreaser => Blue,
                _ => Reset
            };

            Console.WriteLine(
                $"{worker.Name,-12} | " +
                $"{typeColor}{worker.Type,-16}{Reset} | " +
                $"{worker.SuccessOps + worker.FailedOps,8:N0} | " +
                $"{White(worker.TotalContribution.ToString("N0")),-12} | " +
                $"{worker.LastAction,-20}"
            );
        }
        Console.WriteLine(new string('─', 78));

        if(diff > 0) {
            Console.WriteLine($"{Yellow}⚠️  RAM'de birikmiş {diff:N0} veri var. Flush bekleniyor...{Reset}");
        }
        else {
            Console.WriteLine($"{Green}✔  Sistem senkronize veya dış veri akışı var.{Reset}");
        }
    }

    private async Task InputListenerAsync(CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            if(Console.KeyAvailable) {
                var key = Console.ReadKey(true).Key;
                if(key == ConsoleKey.F) {
                    Console.WriteLine($"\n{Yellow}>>> MANUAL FLUSH TRIGGERED...{Reset}");
                    // Service üzerinden tüm bufferları boşalt
                    await counterService.FlushAllAsync(ct);
                }
                else if(key == ConsoleKey.R) {
                    Console.WriteLine($"\n{Red}>>> GLOBAL RESET TRIGGERED...{Reset}");
                    // Her şeyi sıfırla
                    await counterService.ResetAllAsync(ct);
                    // Local istatistikleri de temizle
                    foreach(var w in _workerRegistry.Values) {
                        w.TotalContribution = 0;
                        w.SuccessOps = 0;
                        w.FailedOps = 0;
                    }
                    // Redis başlangıç değerini 0 kabul et
                    _initialRedisValue = 0;
                }
                else if(key == ConsoleKey.Q) {
                    // ApplicationLifetime üzerinden kapatmak daha şık olur ama burada basitçe exception fırlatmayalım
                    Console.WriteLine($"\n{Red}>>> Çıkış yapılıyor...{Reset}");
                    Environment.Exit(0);
                }
            }
            await Task.Delay(100, ct);
        }
    }

    private string White(string text) => $"\x1b[37m{text}{Reset}";
}