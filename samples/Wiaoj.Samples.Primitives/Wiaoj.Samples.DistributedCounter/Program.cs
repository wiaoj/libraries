using System.Collections.Concurrent;
using System.Diagnostics;
using Wiaoj.DistributedCounter;
using Wiaoj.Primitives;

Console.OutputEncoding = System.Text.Encoding.UTF8;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// --- LOGGING AYARLARI ---
builder.Logging.ClearProviders()
    .AddSimpleConsole(o => {
        o.TimestampFormat = "mm:ss.fff "; // Dakika:Saniye.Milisaniye
        o.SingleLine = true;
    });

// --- 1. KÜTÜPHANE KURULUMU ---
builder.Services.AddDistributedCounter(cfg => {
    cfg.Configure(options => {
        options.GlobalKeyPrefix = "test_app:";
        options.AutoFlushInterval = TimeSpan.FromSeconds(5); // 2 saniyede bir Redis'e flush
        options.DefaultStrategy = CounterStrategy.Buffered;  // RAM'de biriktir
    });

    //cfg.UseInMemory();
    // Redis bağlantısı (Kendi connection string'ini yaz)
    cfg.UseRedis("localhost:6379");
});

// --- 2. ORCHESTRATOR'I EKLEME ---
// Bu servis, içeride birden fazla worker task'ı başlatacak.
builder.Services.AddHostedService<MultiWorkerOrchestrator>();

IHost host = builder.Build();
OperationTimeout operationTimeout = OperationTimeout.FromSeconds(60);

CancellationToken token = operationTimeout.CreateCancellationTokenSource().Token;

await host.RunAsync(token);


// --- 3. TEST SENARYOSU ---
// Generic Tag
public class UserVisits { }

public class WorkerStats {
    public long TotalAdded; 
    public long OpCount;   
    public string Name = string.Empty;
}

public class MultiWorkerOrchestrator(
    IDistributedCounter<UserVisits> counter,
    ILogger<MultiWorkerOrchestrator> logger)
    : BackgroundService {

    private const int WORKER_COUNT = 4;
    private readonly ConcurrentDictionary<int, WorkerStats> _workerRegistry = new();
    private readonly Stopwatch _stopwatch = new();
    private long _initialRedisValue = 0;

    // ANSI Renk Kodları
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Cyan = "\x1b[36m";
    private const string Green = "\x1b[32m";
    private const string Yellow = "\x1b[33m";
    private const string Red = "\x1b[31m";
    private const string Magenta = "\x1b[35m";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // Başlangıçta Redis değerini mühürle (Katkı hesaplamak için)
        _initialRedisValue = (await counter.GetValueAsync(stoppingToken)).Value;
        _stopwatch.Start();

        for(int i = 1; i <= WORKER_COUNT; i++) {
            _workerRegistry[i] = new WorkerStats { Name = $"Worker-{i:00}" };
            _ = RunWorkerAsync(i, stoppingToken);
        }

        await RunMonitorAsync(stoppingToken);
    }

    private async Task RunWorkerAsync(int id, CancellationToken ct) {
        var stats = _workerRegistry[id];
        while(!ct.IsCancellationRequested) {
            try {
                long amount = Random.Shared.Next(1, 1000);
                await counter.IncrementAsync(amount, cancellationToken: ct);

                Interlocked.Add(ref stats.TotalAdded, amount);
                Interlocked.Increment(ref stats.OpCount);

                await Task.Delay(Random.Shared.Next(10, 200), ct);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                logger.LogError(ex, "Worker error");
            }
        }
    }

    private async Task RunMonitorAsync(CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            try {
                await Task.Delay(3000, ct); // 3 saniyede bir daha hızlı güncelleme

                var currentVal = await counter.GetValueAsync(ct);
                double elapsed = _stopwatch.Elapsed.TotalSeconds;

                // Hesaplamalar
                long grandTotalSession = _workerRegistry.Values.Sum(x => x.TotalAdded);
                long grandTotalOps = _workerRegistry.Values.Sum(x => x.OpCount);
                long currentRedisVal = currentVal.Value;
                long totalRedisGrowth = currentRedisVal - _initialRedisValue;
                long externalContribution = totalRedisGrowth - grandTotalSession;

                Console.Clear(); // Ekranı temizle (Dashboard hissi)
                Console.WriteLine($"{Bold}{Magenta}🚀 DISTRIBUTED COUNTER REAL-TIME DASHBOARD{Reset}");
                Console.WriteLine($"{Cyan}Uptime: {Reset}{_stopwatch.Elapsed:hh\\:mm\\:ss} | {Cyan}Key: {Reset}{counter.Key}");
                Console.WriteLine(new string('━', 75));

                // GENEL DURUM KARTLARI
                Console.WriteLine($"{Bold}📊 GENEL DURUM{Reset}");
                Console.WriteLine($"  {Green}▶ Redis Global Değer  :{Reset} {currentRedisVal:N0}");
                Console.WriteLine($"  {Green}▶ Bu Session Toplam   :{Reset} +{grandTotalSession:N0}");

                string extColor = externalContribution >= 0 ? Green : Red;
                Console.WriteLine($"  {extColor}▶ Dış Katkı/Sapma     :{Reset} {externalContribution:N0} (Diğer App/Manual)");

                Console.WriteLine($"  {Yellow}▶ Toplam İşlem (Ops)  :{Reset} {grandTotalOps:N0}");
                Console.WriteLine(new string('─', 75));

                // TABLO BAŞLIĞI
                Console.WriteLine($"{Bold}{Cyan}{"WORKER",-12} | {"OPS",-10} | {"CONTRIBUTION",-18} | {"SPEED",-15}{Reset}");
                Console.WriteLine(new string('─', 75));

                // WORKER SATIRLARI
                foreach(var worker in _workerRegistry.Values.OrderBy(x => x.Name)) {
                    double speed = worker.TotalAdded / elapsed;
                    string speedBar = GetSpeedBar(speed);

                    Console.WriteLine(
                        $"{White(worker.Name),-12} | " +
                        $"{worker.OpCount,10:N0} | " +
                        $"{Green + "+" + worker.TotalAdded.ToString("N0"),-18}{Reset} | " +
                        $"{Yellow + speed.ToString("N2"),8} Inc/s {speedBar}"
                    );
                }

                Console.WriteLine(new string('━', 75));

                // ALT BİLGİ (THROUGHPUT)
                double totalThroughput = grandTotalSession / elapsed;
                double opsPerSec = grandTotalOps / elapsed;

                Console.WriteLine($"{Bold}📈 PERFORMANS ANALİZİ{Reset}");
                Console.WriteLine($"  ⚡ {Cyan}Saniyede İşlem (TPS):{Reset} {opsPerSec:F2} ops/sec");
                Console.WriteLine($"  🚀 {Cyan}Saniyede Artış     :{Reset} {totalThroughput:F2} inc/sec");

                if(counter.Strategy == CounterStrategy.Buffered) {
                    Console.WriteLine($"  💎 {Green}MOD: BUFFERED (Yüksek Verimlilik - Redis Dostu){Reset}");
                }

            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                Console.WriteLine($"{Red}Monitor Error: {ex.Message}{Reset}");
            }
        }
    }

    private string GetSpeedBar(double speed) {
        int barLength = (int)Math.Min(speed / 10, 10); // Hıza göre bar boyutu
        return new string('█', barLength).PadRight(10, '░');
    }

    private string White(string text) => $"\x1b[37m{text}{Reset}";
}