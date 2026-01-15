using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using static LoadTestWorker; 

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders()
    .AddSimpleConsole(o => {
        // Detaylı Tarih Formatı: Yıl-Ay-Gün Saat:Dakika:Saniye.Milisaniye
        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";

        // Zaman damgasının görünmesi için UseUtcTimestamp seçeneğini de belirleyebilirsin
        o.UseUtcTimestamp = false; // Yerel saat için false, UTC için true

        o.SingleLine = true; // Logların daha derli toplu görünmesini sağlar
    });


// --- 1. KÜTÜPHANE KURULUMU ---
builder.Services.AddDistributedCounter(cfg => {

    // Core Ayarları
    cfg.Configure(options => {
        options.GlobalKeyPrefix = "test_app:";
        options.AutoFlushInterval = TimeSpan.FromSeconds(2); // 2 saniyede bir Redis'e yaz
        options.DefaultStrategy = CounterStrategy.Buffered;  // Varsayılan olarak RAM'de biriktir
    });

    // Storage (Redis) Ayarı
    cfg.UseRedis("localhost:6379");
});

// --- 2. TEST WORKER'I EKLEME ---
builder.Services.AddHostedService<LoadTestWorker>();

IHost host = builder.Build();
host.Run();


// --- 3. TEST SENARYOSU ---
// Bu sınıf sanki bir API Controller veya başka bir servismiş gibi davranacak.
public class LoadTestWorker(
    IDistributedCounter<UserVisits> counter,
    ILogger<LoadTestWorker> logger)
    : BackgroundService {

    private readonly string _workerId = Guid.NewGuid().ToString()[..4];
    public class UserVisits { }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation("[{WorkerId}] 🚀 Test Başlıyor! Key: {Key}", _workerId, counter.Key);

        // İsteğe bağlı: Başlangıçta Redis'teki mevcut değeri çekelim
        long initialValue = await counter.GetValueAsync(stoppingToken);
        logger.LogInformation("[{WorkerId}] 📊 Mevcut Global Değer: {Value}", _workerId, initialValue);

        while(!stoppingToken.IsCancellationRequested) {
            try {
                // 1. Rastgele bir artış miktarı belirle
                long amount = Random.Shared.Next(1, 1);

                // 2. Artır (Buffered olduğu için RAM'de birikir)
                var localResult = await counter.IncrementAsync(amount, ct: stoppingToken);

                // 3. Logla
                logger.LogInformation("[{WorkerId}] ➕ (+{Amount}) -> Yerel Buffer: {Total}",
                    _workerId, amount, localResult);

                // 4. Belirli aralıklarla (örneğin her 10 saniyede bir) Global değeri kontrol et
                if(DateTime.Now.Second % 10 == 0) {
                    var globalValue = await counter.GetValueAsync(stoppingToken);
                    logger.LogWarning("[{WorkerId}] 🌍 REDIS GLOBAL: {GlobalValue}", _workerId, globalValue);
                }
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                logger.LogError(ex, "[{WorkerId}] ❌ Hata oluştu!", _workerId);
            }

            await Task.Delay(1000, stoppingToken); // 1 saniyeye düşürdüm ki buffer doluşunu izleyebilesin
        }
    }
}