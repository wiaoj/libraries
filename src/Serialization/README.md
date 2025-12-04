### Schema

Orleans ile Schema Yönetimi ???

bulut serileştir servisi

protobuf, avro, schema registry?
---

# Wiaoj.Serialization

## 👋 Kütüphane Vizyonumuz

**Kütüphane Adı:** Wiaoj.Serialization

**Misyonumuz:** .NET geliştiricilerinin, serileştirme süreçlerini merkezi, yönetilebilir, esnek ve yüksek performanslı iş akışları (workflows) halinde kurmalarını sağlayan, sınıfının en iyisi bir serileştirme yönetim çatısı (framework) olmak.

**Temel Değer Teklifimiz:**
*   **Mükemmel Esneklik:** Tek bir proje içinde birden fazla serileştirici ve politika (JSON, MessagePack, XML, CSV, YAML vb.) yönetimi.
*   **Sıfır Overhead Soyutlama:** Kaynak Üreticileri (Source Generators) ve optimize edilmiş DI ile çalışma zamanında (runtime) soyutlamadan kaynaklanan performans kaybını ortadan kaldırma.
*   **Merkezi Politika Yönetimi:** Serileştirme süreçlerine şifreleme, sıkıştırma, loglama, doğrulama, telemetri gibi kesişen ilgi alanlarını (cross-cutting concerns) kolayca entegre etme.
*   **Basit ve Akıcı API:** Geliştiricinin karmaşık detaylarla uğraşmadan, tek satırda serileştirme politikalarını tanımlamasını sağlama.
*   **Production Ready:** Güçlü test altyapısı, kapsamlı dokümantasyon ve modern .NET standartlarına tam uyumluluk.

---

## 🚀 Başlarken (Getting Started)

Bu bölüm, kütüphanemizin temel kullanımını ve kurulumunu en hızlı şekilde gösterir.

### 1. Kurulum (NuGet Paketleri)

```bash 
dotnet add package Wiaoj.Serialization.SystemTextJson # veya Newtonsoft, MessagePack, vb.
 
```

### 2. Varsayılan Serileştiriciyi Kaydetme (Program.cs)

```csharp
// Program.cs

var builder = WebApplication.CreateBuilder(args);

// 1. Wiaoj.Serialization'ı hizmetlere ekle
builder.Services.AddWiaojSerializer(options =>
{
    // 2. Varsayılan serileştirici olarak System.Text.Json'ı SystemTextJson anahtarını kullanarak kaydet
    options.UseSystemTextJson<DefaultSerializerKey>(stjOptions =>
    {
        stjOptions.WriteIndented = true;
        stjOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

    // İsteğe bağlı: Telemetriyi etkinleştir
    // options.AddTelemetry(); // Eğer Wiaoj.Serialization.Observability.OpenTelemetry paketi yüklüyse
});
 
var app = builder.Build();

// ... diğer pipeline ayarları ...

app.Run();
```

### 3. Controller'da Kullanım

```csharp
[ApiController]
[Route("[controller]")]
public class ProductsController : ControllerBase
{
    // Varsayılan serileştiriciyi enjekte et (TKey belirtmeden ISerializer<DefaultSerializerKey>)
    private readonly ISerializer<DefaultSerializerKey> _serializer;

    public ProductsController(ISerializer<DefaultSerializerKey> serializer)
    {
        _serializer = serializer;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var products = new List<Product> { new Product { Id = 1, Name = "Test" } };

        // Serileştiriciyi doğrudan kullanabiliriz (byte[], string veya stream olarak)
        var jsonString = _serializer.SerializeToString(products); // Varsayılan ayarlar JSON'a çevirir

        return Ok(products); // ASP.NET Core, UseWiaojSerializerAsApiEngine sayesinde bizim serializer'ımızı kullanır
    }
}
```

---

## 📚 İleri Seviye Kullanım ve Mimariler

Bu bölüm, kütüphanenin tam potansiyelini gösterir.

### 1. Birden Fazla Serileştirici / Politika Tanımlama

```csharp
services.AddWiaojSerializer(options =>
{
    // API yanıtları için System.Text.Json
    options.UseSystemTextJson<ApiResponsesKey>(stj => { /* API JSON ayarları */ });

    // Redis Cache için MessagePack (sıkıştırmalı ve telemetrili)
    options.UseMessagePack<CacheKey>(mpConfig =>
    {
        mpConfig.Pipeline
            .Add<Lz4CompressionHandler>() // Varsayılan sıkıştırma
            .AddTelemetry();                // Telemetriyi de ekle
    });

    // CSV raporları için
    options.UseCsvHelper<ReportCsv>(csvConfig => { /* CSV ayarları */ });
});

// Controller'da Kullanım
public class MyService(ISerializer<ApiResponsesKey> apiSerializer, ISerializer<CacheKey> cacheSerializer)
{
    public void SaveToCache(object data) => cacheSerializer.Serialize("cache_key", data);
    public string GetApiJson(object data) => apiSerializer.SerializeToString(data);
}
```

### 2. Pipeline ile Davranış Ekleme

```csharp
// Örnek Handler'lar (Örnek olması için namespace'leri farklı varsayalım)
// using Wiaoj.Serialization.Handlers.Security;
// using Wiaoj.Serialization.Handlers.Compression;
// using Wiaoj.Serialization.Observability;

services.AddWiaojSerializer(options =>
{
    options.UseSystemTextJson<SecureJson>(config =>
    {
        config.Pipeline
            // Handler'ları istediğiniz sırada ekleyin
            .Add<TelemetryHandler>()              // Önce telemetriyi kaydet
            .Add<PiiScrubbingHandler>()           // Sonra hassas verileri maskele
            .Add<AesEncryptionHandler>()          // Sonra şifrele
            // Son olarak çekirdek serileştirme işlemi
            // (Çekirdek handler otomatik olarak eklenir)
    });
});
```

### 3. ASP.NET Core Entegrasyonu (API Controller & Minimal API)

*   **API Controller'lar İçin:**
    ```csharp
    builder.Services.AddControllers()
        .UseWiaojSerializerAsApiEngine<ApiResponsesKey>();
    ```

*   **Minimal API'ler İçin:**
    ```csharp
    app.MapGet("/data", (ISerializer<ApiResponsesKey> serializer) =>
    {
        var data = new { Message = "Hello" };
        // Açık kullanım
        return Results.Stream(stream => serializer.SerializeAsync(stream, data), "application/json");
    });

    // Veya daha zarif özel Result ile:
    app.MapGet("/data-elegant", (ISerializer<ApiResponsesKey> serializer) =>
    {
        var data = new { Message = "Hello" };
        return WiaojResults.Json<ApiResponsesKey>(data);
    });
    ```

### 4. Kaynak Üreticileri (Source Generators)

*   **Vizyon:** Derleme zamanında, `ISerializer<TKey>` arayüzü kullanımlarını, doğrudan optimize edilmiş somut sınıf çağrılarına dönüştürmek. Bu, performans overhead'ini sıfırlar ve AOT/Trimming uyumluluğunu sağlar.
*   **Mevcut Durum:** Bu, projemizin ilerleyen aşamalarında (v2.0 hedefi) tam olarak hayata geçirilecektir. README'de bu hedefin altı çizilmelidir.

---

## 🚀 Performans ve Gözlemlenebilirlik

### 1. Performans: Neredeyse Sıfır Overhead

*   **Çıplak Kurulum:** Kullanıcı hiçbir handler eklemezse, `NakedSerializer` sayesinde doğrudan çekirdek motor çağrılır.
*   **Pipeline Kullanımı:** Eklenen her handler için küçük bir maliyet olur.
*   **Kaynak Üreticileri:** Derleme zamanında üretilen kod ile bu maliyet, çalışma zamanında **pratik olarak sıfıra indirilir.** Bu hedefin altı çizilmelidir.
*   **Benchmark Verileri:** README'de, farklı senaryolardaki (boş pipeline vs dolu pipeline) benchmark sonuçları yayınlanacaktır.

### 2. Gözlemlenebilirlik (OpenTelemetry)

*   **Otomatik Telemetri:** `Wiaoj.Serialization.Observability.OpenTelemetry` paketi ile, kullanıcılar pipeline'larına `AddTelemetry()` ekleyerek tüm serileştirme/deserileştirme işlemlerini (süre, boyut, hata, serializer tipi, anahtar vb.) otomatik olarak OpenTelemetry compatible trace'ler ve metrikler olarak izleyebilirler.
*   **Tek Activity:** Kaynak Üreticisi sayesinde, pipeline'ın tamamı (tüm handler'lar dahil) tek bir ana Activity içinde izlenecektir.

---

## 🛡️ Güvenlik ve Uyumluluk (Security & Compliance)

*   **PII Maskeleme:** Loglama sırasında hassas verileri otomatik olarak maskelemek için `PiiScrubbingHandler` gibi handler'lar sunulabilir.
*   **Merkezi Politika Uygulama:** Kurumsal güvenlik politikalarının (örn. belirli verilerin şifrelenmesi), pipeline'lar aracılığıyla zorunlu kılınması.

---

## ❓ SSS (Sıkça Sorulan Sorular)

*   **Neden `ISerializer<TKey>` yerine somut sınıfları kullanmıyoruz?**
    *   Test edilebilirliği, esnekliği ve değiştirilebilirliği korumak için. Kaynak Üreticileri, bu soyutlamanın performans maliyetini ortadan kaldırır.
*   **Pipeline'ın performansa etkisi nedir?**
    *   Boş pipeline'da sıfır, dolu pipeline'da ise eklenen handler'ların maliyeti kadar. Kaynak Üreticileri ile bu maliyet minimuma iner.
*   **Hangi serileştiricileri destekliyorsunuz?**
    *   Önceliklerimiz: System.Text.Json, Newtonsoft.Json. Ardından MessagePack, XML, CSV, Protobuf, YAML gelecek.
*   **OpenTelemetry entegrasyonu varsayılan olarak mı geliyor?**
    *   Hayır, kullanıcı bilinçli olarak `AddTelemetry()`'yi çağırarak etkinleştirmelidir, ancak bu işlem çok kolaydır.

---

## 🤝 Katkıda Bulunmak (Contributing)

[Katkıda bulunma yönergeleri, kodlama standartları, test kılavuzları buraya eklenecek.]

---

## ⚖️ Lisans

[Lisans bilgisi buraya eklenecek (örn. MIT)]

---

Bu README yapısı, kütüphanemizin ne olduğunu, neden değerli olduğunu, nasıl kullanılacağını ve hangi güçlü özelliklere sahip olduğunu kapsamlı bir şekilde anlatacaktır. Geliştiricilerin bizi anlamasını, güvenmesini ve kullanmaya başlamasını kolaylaştıracaktır.

Şimdi bu planı hayata geçirme zamanı!