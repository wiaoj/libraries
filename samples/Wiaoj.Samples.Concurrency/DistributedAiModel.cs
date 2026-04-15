using System.Net.Http.Headers;

namespace Wiaoj.Samples.Concurrency;

// IAsyncDisposable implemente ediyor çünkü AsyncLazy<T> disposal sırasında bunu otomatik çağıracak!
public sealed class DistributedAiModel : IAsyncDisposable {
    private readonly HttpClient _httpClient;
    private readonly byte[] _heavyModelWeights; // Simüle edilmiş devasa veri

    // Constructor PRIVATE! Dışarıdan 'new' keyword'ü ile senkron olarak yaratılmasını yasaklıyoruz.
    private DistributedAiModel(HttpClient httpClient, byte[] weights) {
        this._httpClient = httpClient;
        this._heavyModelWeights = weights;
    }

    // Factory metodumuz. AsyncLazy sadece bu metodu çağıracak.
    public static async Task<DistributedAiModel> InitializeAsync(string modelUrl, CancellationToken ct) {
        Console.WriteLine("\n[AĞIR İŞLEM] Modeli başlatma süreci tetiklendi...");
        HttpClient client = new();

        // Adım 1: Kimlik doğrulama simülasyonu
        Console.WriteLine("[AĞIR İŞLEM] 1/3: Auth sunucusuna bağlanılıyor ve token alınıyor...");
        await Task.Delay(1500, ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "super-secret-token");

        // Adım 2: Devasa model dosyasını indirme simülasyonu
        Console.WriteLine("[AĞIR İŞLEM] 2/3: 5GB'lık model ağırlıkları ağ üzerinden indiriliyor...");
        await Task.Delay(3000, ct); // Ağ gecikmesi

        // İptal edildiyse işlemi yarıda kes! (AsyncLazy bu exception'ı yakalayıp fault state'e geçer)
        ct.ThrowIfCancellationRequested();

        var dummyWeights = new byte[1024]; // Bellekte yer kaplayan veriyi simüle ediyoruz

        // Adım 3: Matris hesaplamaları (CPU Bound)
        Console.WriteLine("[AĞIR İŞLEM] 3/3: Donanım hızlandırma (GPU) matrisleri hazırlanıyor...");
        await Task.Delay(1000, ct);

        Console.WriteLine("[BAŞARILI] Model başarıyla ayağa kalktı!\n");
        return new DistributedAiModel(client, dummyWeights);
    }

    public void Predict(string inputData) {
        // Modelin asıl işi
        Console.WriteLine($"[Tahmin] '{inputData}' işlendi.");
    }

    // Sistem kapandığında AsyncLazy tarafından otomatik olarak tetiklenecek.
    public async ValueTask DisposeAsync() {
        Console.WriteLine("\n[TEMİZLİK] Model bellekten atılıyor, HTTP bağlantıları kapatılıyor...");
        await Task.Delay(500); // Kapanış süreci
        this._httpClient.Dispose();
        Console.WriteLine("[TEMİZLİK] Sistem güvenli bir şekilde kapatıldı.");
    }
}