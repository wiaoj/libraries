using Wiaoj.Concurrency;
using Wiaoj.Samples.Concurrency;

//HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

//builder.Services.AddHostedService<Worker>();

//builder.Services.AddSingleton(sp => {
//    return new AsyncLazy<DistributedAiModel>(async (CancellationToken ct) => {
//        return await DistributedAiModel.InitializeAsync("https://api.my-enterprise-ai.com/v1/models/gpt", ct);
//    });
//});

//// 2. Uygulamayı (Host) inşa et ve kapsamdan çıkarken otomatik temizlik (Dispose) yapması için 'using' ile tanımla.
//using IHost host = builder.Build();

//// 3. Oluşturduğumuz AsyncLazy nesnesini DI'dan çek
//AsyncLazy<DistributedAiModel> lazyAiModel = host.Services.GetRequiredService<AsyncLazy<DistributedAiModel>>();

//Console.WriteLine("Program.cs: 100 farklı istek aynı anda modele saldırmak üzere...");
//await Task.Delay(1000);

//// Uygulama içi iptal işlemleri için
//using var cts = new CancellationTokenSource();
//var tasks = new List<Task>();

//// 4. Worker'ın işini doğrudan burada yapıyoruz
//for(int i = 1; i <= 100; i++) {
//    int taskId = i;
//    tasks.Add(Task.Run(async () => {
//        try {
//            // İŞTE BÜYÜ BURADA: 100 thread aynı anda bu satıra vuracak.
//            DistributedAiModel model = await lazyAiModel.GetValueAsync(cts.Token);
//            model.Predict($"Task-{taskId}");
//        }
//        catch(OperationCanceledException) {
//            Console.WriteLine($"Task-{taskId} iptal edildi.");
//        }
//    }));
//}

//// 5. Tüm thread'lerin modelden cevap almasını ve bitmesini bekle
//await Task.WhenAll(tasks);

//Console.WriteLine("\nTüm istekler başarıyla yanıtlandı!");
//Console.WriteLine("Program sonlanıyor. Host 'using' bloğundan çıkarken Model'in Dispose() metodu otomatik tetiklenecek...\n");


Console.WriteLine("Sistem başlatılıyor...\n");

await RunAsyncLockExample();
await RunStripedLockExample();
await RunAsyncBarrierExample();
await RunAsyncAutoResetEventExample();

Console.WriteLine("\nTüm senaryolar başarıyla tamamlandı");

// ====================================================================
// 1. AsyncLock Senaryosu: Klasik Kritik Bölge Koruması
// ====================================================================
static async Task RunAsyncLockExample() {
    Console.WriteLine("--- 1. AsyncLock Testi ---");
    Console.WriteLine("Açıklama: 100 farklı işlem aynı anda ortak bir sayacı artırmaya çalışacak.");

    var asyncLock = new AsyncLock();
    int sharedCounter = 0;
    var tasks = new List<Task>();

    for(int i = 0; i < 100; i++) {
        tasks.Add(Task.Run(async () => {
            // Eğer lock olmazsa sonuç 100 çıkmaz, veriler ezilir (Race Condition).
            // 'using' bloğu, işlem bittiğinde Scope nesnesini Dispose ederek kilidi serbest bırakır.
            using(await asyncLock.LockAsync()) {
                int temp = sharedCounter;
                await Task.Delay(1); // Ağ veya I/O gecikmesi simülasyonu
                sharedCounter = temp + 1;
            }
        }));
    }

    await Task.WhenAll(tasks);
    Console.WriteLine($"İşlem bitti. Beklenen Sayaç: 100, Gerçekleşen: {sharedCounter}\n");
}

// ====================================================================
// 2. StripedLock Senaryosu: Bölünmüş Kilit ile Yüksek Performans
// ====================================================================
static async Task RunStripedLockExample() {
    Console.WriteLine("--- 2. StripedLock Testi ---");
    Console.WriteLine("Açıklama: Farklı kullanıcılara (ID'lere) ait cüzdan güncellemeleri yapılıyor.");
    Console.WriteLine("Farklı ID'ler birbirini BEKLEMEYECEK, sadece AYNI ID'ye gelen istekler sıraya girecek.");

    // 128 adet alt kilit oluşturulur (2'nin kuvveti olmalı).
    var stripedLock = new StripedLock<string>(128);
    var tasks = new List<Task>();

    // Ali ve Ayşe için aynı anda birden fazla bakiye güncelleme isteği geliyor
    string[] userIds = { "user_ali", "user_ayse", "user_ali", "user_ayse", "user_veli" };

    foreach(var id in userIds) {
        tasks.Add(Task.Run(async () => {
            // LockAsync, string ID'nin HashCode'una göre 128 kilitten birini seçer.
            using(await stripedLock.LockAsync(id)) {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {id} için bakiye güncelleniyor...");
                await Task.Delay(500); // 500ms süren ağır bir veritabanı işlemi
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {id} işlemi bitti.");
            }
        }));
    }

    await Task.WhenAll(tasks);
    Console.WriteLine("StripedLock testi bitti. Farklı kullanıcıların işlemleri paralel, aynı olanlar ardışık çalıştı.\n");
}

// ====================================================================
// 3. AsyncBarrier Senaryosu: Aşamalı İşlem Koordinasyonu
// ====================================================================
static async Task RunAsyncBarrierExample() {
    Console.WriteLine("--- 3. AsyncBarrier Testi ---");
    Console.WriteLine("Açıklama: 3 farklı sunucudan veri indirilecek. Hepsi indirmeyi bitirmeden 'Birleştirme' aşamasına geçilemez.");

    int participantCount = 3;
    var barrier = new AsyncBarrier(participantCount);
    var tasks = new List<Task>();

    for(int i = 1; i <= participantCount; i++) {
        int serverId = i;
        tasks.Add(Task.Run(async () => {
            // Aşama 1: Veri İndirme
            int delay = new Random().Next(500, 1500);
            await Task.Delay(delay);
            Console.WriteLine($"Sunucu {serverId}: Veri indirmeyi tamamladı. Diğerlerini bekliyor...");

            // DİKKAT: Diğer 2 sunucu da buraya gelene kadar kod aşağıya inmeyecek!
            await barrier.SignalAndWaitAsync();

            // Aşama 2: Veri Birleştirme (Sadece hepsi indirmeyi bitirince buraya geçerler)
            Console.WriteLine($"Sunucu {serverId}: Veri birleştirme aşamasına başladı.");
            await Task.Delay(500);

            await barrier.SignalAndWaitAsync();
            Console.WriteLine($"Sunucu {serverId}: İşlemi tamamen bitirdi.");
        }));
    }

    await Task.WhenAll(tasks);
    Console.WriteLine("Tüm sunucular eşgüdümlü olarak çalışmasını tamamladı.\n");
}

// ====================================================================
// 4. AsyncAutoResetEvent Senaryosu: Sinyal Bekleme (Producer/Consumer)
// ====================================================================
static async Task RunAsyncAutoResetEventExample() {
    Console.WriteLine("--- 4. AsyncAutoResetEvent Testi ---");
    Console.WriteLine("Açıklama: İşçi bir thread uyuyor. Sadece 'Set()' sinyali geldiğinde uyanıp tek bir iş yapacak.");

    // Başlangıçta unset (false) durumunda.
    var resetEvent = new AsyncAutoResetEvent(false);

    // Tüketici (Consumer) - Arka planda sürekli sinyal bekler
    var consumerTask = Task.Run(async () => {
        for(int i = 1; i <= 3; i++) {
            Console.WriteLine("İşçi: Uyuyorum. Sinyal bekliyorum...");
            await resetEvent.WaitAsync(); // Sinyal gelene kadar async olarak bekler (CPU tüketmez)

            Console.WriteLine($"İşçi: Sinyal aldım! {i}. paketi işliyorum...");
            await Task.Delay(300); // İş yapılıyor
        }
    });

    // Üretici (Producer) - Aralıklı olarak sinyal gönderir
    var producerTask = Task.Run(async () => {
        for(int i = 1; i <= 3; i++) {
            await Task.Delay(1000); // 1 saniye bekle
            Console.WriteLine($"Patron: {i}. paket hazır. İşçiye sinyal gönderiliyor!");
            resetEvent.Set(); // Bekleyen bir task'i serbest bırakır ve anında geri kapanır (Auto Reset).
        }
    });

    await Task.WhenAll(consumerTask, producerTask);
    Console.WriteLine("Sinyalleşme senaryosu tamamlandı.\n");
}