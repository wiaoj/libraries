# Wiaoj.Webhooks

> Distributed, uçtan uca, %100 genişletilebilir webhook gönderim ve alım altyapısı — .NET için.

Wiaoj ekosistemindeki `DistributedCounter`, `Tyto.Transports`, `Tyto.DeadLettering` ve `Wiaoj.Security` üzerine kurulu; hiçbir varsayılanı zorunlu kılmayan, her katmanı ayrı ayrı değiştirilebilir bir webhook motoru.

---

## Felsefe

Bu kütüphane üç ilkeye sıkı sıkıya bağlı kalınarak tasarlanmıştır:

1. **Her şey bir interface arkasında yaşar.** Signing, retry, rate limit, transport, idempotency, delivery log — hiçbiri somut bir sınıfa hardcode edilmez. Varsayılan implementasyonlar sunulur, ama hiçbiri zorunlu değildir.
2. **Konfigürasyon `IOptions<T>` ile tamamen uyumludur.** `appsettings.json`'dan, `IConfiguration`'dan, `IOptionsMonitor<T>` ile runtime'da değişen değerlerden — hepsi desteklenir. Kod içi builder ile konfigüre edilen her şey, aynı zamanda dışarıdan da override edilebilir olmalıdır.
3. **Builder pattern, mevcut Tyto/Wiaoj sözleşmesini kırmaz.** `tyto.AddRpc(rpc => ...)` nasıl çalışıyorsa, `services.AddWiajWebhooks(webhooks => ...)` da aynı zihniyetle çalışır. Yeni bir öğrenme eğrisi yaratmak yerine, var olanı genişletir.

---

## İçindekiler

- [Paket Mimarisi](#paket-mimarisi)
- [Hızlı Başlangıç](#hızlı-başlangıç)
- [Çekirdek Soyutlamalar](#çekirdek-soyutlamalar)
- [Options Pattern Uyumu](#options-pattern-uyumu)
- [Pipeline Mimarisi](#pipeline-mimarisi)
- [Genişletme Noktaları](#genişletme-noktaları)
- [TDD Roadmap: A'dan Z'ye](#tdd-roadmap-adan-zye)
- [Bu Kütüphane Neyin Kapısını Açıyor?](#bu-kütüphane-neyin-kapısını-açıyor)

---

## Paket Mimarisi

```
Wiaoj.Webhooks.Abstractions
    └─ Sıfır bağımlılık. Sadece interface + record + enum.

Wiaoj.Webhooks
    └─ Core engine: pipeline runner, default implementasyonlar, in-memory fallback'ler.

Wiaoj.Webhooks.Signing.Hmac
Wiaoj.Webhooks.Signing.Ed25519
    └─ IWebhookSigner implementasyonları (opsiyonel, takılabilir).

Wiaoj.Webhooks.RateLimiting
    └─ Wiaoj.DistributedCounter üzerine kurulu IWebhookRateLimiter.

Wiaoj.Webhooks.Transports.Postgres
Wiaoj.Webhooks.Transports.RabbitMq
Wiaoj.Webhooks.Transports.Kafka
Wiaoj.Webhooks.Transports.InMemory
    └─ Tyto.Transports.* üzerine kurulu IWebhookTransport implementasyonları.

Wiaoj.Webhooks.DeadLettering
    └─ Tyto.DeadLettering entegrasyonu.

Wiaoj.Webhooks.Persistence.EntityFrameworkCore
    └─ Delivery log, endpoint kaydı, attempt history için EF Core store.

Wiaoj.Webhooks.Inbound.AspNetCore
    └─ Gelen webhook'ları karşılama, signature doğrulama middleware'i.

Wiaoj.Webhooks.Inbound.Providers.Stripe
Wiaoj.Webhooks.Inbound.Providers.GitHub
    └─ Sağlayıcıya özel signature/format adaptörleri.

Wiaoj.Webhooks.DependencyInjection
    └─ AddWiajWebhooks(...) builder giriş noktası, tüm alt paketleri birbirine bağlar.

Wiaoj.Webhooks.Testing
    └─ TDD için: InMemoryWebhookTransport, FakeClock, deterministic retry test yardımcıları.
```

**Kural:** `Wiaoj.Webhooks` çekirdek paketi hiçbir zaman Redis, Postgres, RabbitMq gibi somut bir teknolojiye referans vermez. Bunlar hep satellite paketlerdedir. Çekirdek sadece interface'leri bilir.

---

## Hızlı Başlangıç

```csharp
builder.Services.AddWiajWebhooks(webhooks =>
{
    webhooks.UseSigning(s => s.UseHmacSha256());
    webhooks.UseTransport("postgres");
    webhooks.UseRateLimiting(rl => rl.PerEndpoint(limit: 50, window: TimeSpan.FromSeconds(1)));
    webhooks.UseDeadLettering();
});
```

```csharp
public sealed record OrderCreatedWebhookEvent(string OrderId, decimal Amount) : IWebhookEvent
{
    public static string EventName => "order.created";
}

// Gönderim
await dispatcher.DispatchAsync(endpointId, new OrderCreatedWebhookEvent("ORD-1", 42.50m), ct);
```

Bu iki blok dışında hiçbir şey yazmadan; imzalama, retry, rate limit, dead letter, delivery log otomatik çalışır. İstenirse her biri tek tek override edilebilir.

---

## Çekirdek Soyutlamalar

### Olay tanımı — tip güvenli, magic string yok

```csharp
public interface IWebhookEvent
{
    static abstract string EventName { get; }
}
```

`Prism.AspNetCore`'daki `IPrismRateLimitPolicy` (`static abstract string PolicyName`) pattern'inin birebir devamı — aynı disiplin, aynı imza stili.

### Dispatcher — tek giriş noktası

```csharp
public interface IWebhookDispatcher
{
    Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        WebhookDispatchOptions? overrides = null,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent;
}
```

### Signer — algoritma tamamen değiştirilebilir

```csharp
public interface IWebhookSigner
{
    WebhookSignature Sign(ReadOnlySpan<byte> payload, string secret, DateTimeOffset timestamp);
    bool Verify(ReadOnlySpan<byte> payload, string signatureHeader, string secret);
}
```

### Retry stratejisi — pluggable

```csharp
public interface IRetryPolicy
{
    bool ShouldRetry(WebhookDeliveryAttempt attempt);
    TimeSpan GetNextDelay(WebhookDeliveryAttempt attempt);
}
```

### Rate limiter — DistributedCounter'ı sarmalar, ama zorunlu değil

```csharp
public interface IWebhookRateLimiter
{
    ValueTask<RateLimitDecision> TryAcquireAsync(
        WebhookEndpointId endpointId,
        CancellationToken cancellationToken = default);
}
```

### Transport — nereye kuyruklanacağı

```csharp
public interface IWebhookTransport
{
    Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay = null, CancellationToken cancellationToken = default);
}
```

### Idempotency

```csharp
public interface IIdempotencyKeyGenerator
{
    string GenerateKey(WebhookEndpointId endpointId, IWebhookEvent @event);
}

public interface IIdempotencyStore
{
    ValueTask<bool> TryMarkProcessedAsync(string key, TimeSpan window, CancellationToken cancellationToken = default);
}
```

### Delivery log — gözlemlenebilirlik

```csharp
public interface IWebhookDeliveryStore
{
    Task RecordAttemptAsync(WebhookDeliveryAttempt attempt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookDeliveryAttempt>> GetHistoryAsync(WebhookDeliveryHandle handle, CancellationToken cancellationToken = default);
}
```

Her interface **tek sorumluluk** taşır. Hiçbiri diğerinin varlığını bilmez — pipeline onları birbirine bağlar.

---

## Options Pattern Uyumu

Builder API'si sadece `IOptions<T>` üzerine ince bir syntactic sugar katmanıdır. Her `Use...` çağrısı aslında arkada bir options sınıfını dolduruyor:

```csharp
public sealed class WebhookRetryOptions
{
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromHours(2);
    public int MaxAttempts { get; set; } = 8;
    public JitterStrategy Jitter { get; set; } = JitterStrategy.Full;
}
```

Bu sayede aynı ayar üç farklı yoldan yapılabilir, hepsi eşdeğerdir:

```csharp
// 1. Builder ile (kod içi, derleme zamanı güvenli)
webhooks.UseRetry(r => r.UseExponentialBackoff(o => o.MaxAttempts = 10));

// 2. appsettings.json ile
services.Configure<WebhookRetryOptions>(configuration.GetSection("Webhooks:Retry"));

// 3. Runtime'da IOptionsMonitor ile canlı değişim
services.Configure<WebhookRetryOptions>(o => o.MaxAttempts = 10);
```

```json
{
  "Webhooks": {
    "Retry": {
      "InitialDelay": "00:00:30",
      "MaxAttempts": 10,
      "Jitter": "Full"
    }
  }
}
```

`IWebhookRateLimiter`, `IRetryPolicy` gibi servisler `IOptionsMonitor<T>` inject ederek çalışır, böylece **restart gerekmeden** limit/backoff değerleri production'da güncellenebilir.

---

## Pipeline Mimarisi

Gönderim akışı, ASP.NET Core middleware'ine benzer şekilde **outbound pipeline** olarak kurulur. Sıra değiştirilebilir, adım eklenip çıkarılabilir:

```csharp
webhooks.UsePipeline(pipeline =>
{
    pipeline.Use<IdempotencyMiddleware>();
    pipeline.Use<RateLimitMiddleware>();
    pipeline.Use<SigningMiddleware>();
    pipeline.Use<CustomAuditMiddleware>();   // kullanıcı kendi middleware'ini ekleyebilir
    pipeline.Use<HttpDeliveryMiddleware>();
    pipeline.Use<RetrySchedulingMiddleware>();
});
```

Her middleware aynı sözleşmeye uyar:

```csharp
public interface IWebhookMiddleware
{
    Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next);
}
```

`WebhookDeliveryContext` içinde payload, endpoint, attempt geçmişi, cancellation token ve middleware'lerin birbirine veri taşıyabileceği bir `Items` dictionary'si bulunur — tıpkı `HttpContext.Items` gibi.

**Neden bu şekilde:** Her middleware izole test edilebilir (`IdempotencyMiddleware`'i tek başına, sahte `next` delegesiyle test edebilirsiniz), sıralama açıkça görülebilir, ve yeni bir gereksinim geldiğinde (mesela "gönderim öncesi payload'ı zenginleştir") mevcut hiçbir şeyi bozmadan araya yeni middleware eklenebilir.

---

## Genişletme Noktaları

| Ne değiştirmek istiyorsun? | Hangi interface'i implemente et |
|---|---|
| İmzalama algoritmasını | `IWebhookSigner` |
| Retry/backoff stratejisini | `IRetryPolicy` |
| Rate limit algoritmasını | `IWebhookRateLimiter` |
| Kuyruklama backend'ini | `IWebhookTransport` |
| Idempotency key üretimini | `IIdempotencyKeyGenerator` |
| Idempotency depolamayı | `IIdempotencyStore` |
| Delivery log depolamayı | `IWebhookDeliveryStore` |
| Pipeline'a yeni adım eklemeyi | `IWebhookMiddleware` |
| Gelen webhook doğrulamasını | `IInboundSignatureVerifier` |
| Payload serileştirmeyi | `IWebhookPayloadSerializer` |

Her satırdaki interface, `Wiaoj.Webhooks.Abstractions`'da tanımlıdır ve DI konteynerine `services.Replace<TInterface, TImplementation>()` ile veya builder üzerinden (`webhooks.UseSigner<TCustomSigner>()`) enjekte edilir. Kütüphanenin hiçbir yerinde `new HmacSigner()` gibi somut bir tip hardcode edilmez.

---

## TDD Roadmap: A'dan Z'ye

Her faz, önce testlerle başlar (`Wiaoj.Webhooks.Testing` paketindeki sahte implementasyonlarla), sonra gerçek implementasyon yazılır. Hiçbir faz bir öncekini bozmadan tamamlanmalıdır.

### Faz 0 — Abstractions İskeleti
- [ ] Tüm interface'leri, record'ları, enum'ları `Wiaoj.Webhooks.Abstractions`'a yaz.
- [ ] Sıfır implementasyon, sadece sözleşme.
- [ ] `Wiaoj.Webhooks.Testing`: `InMemoryWebhookTransport`, `FakeTimeProvider`, `NullSigner` gibi test double'ları hazırla.
- **Çıktı:** Derlenen ama çalışmayan bir iskelet. Testler henüz kırmızı.

### Faz 1 — Çekirdek Dispatch (Signing + Transport olmadan)
- [ ] `WebhookDispatcher.DispatchAsync` — sadece `IWebhookTransport.EnqueueAsync` çağırsın.
- [ ] Test: "Dispatch edilen event, transport'a doğru payload ile ulaşıyor mu?"
- [ ] `InMemoryWebhookTransport` ile tamamen izole, gerçek ağ/DB olmadan test edilir.
- **Çıktı:** `dispatcher.DispatchAsync(...)` çalışıyor, ama henüz HTTP göndermiyor.

### Faz 2 — HTTP Delivery Middleware
- [ ] `HttpDeliveryMiddleware` — `HttpClient` ile gerçek POST atar.
- [ ] Test: `HttpMessageHandler` mock'lanarak 200/4xx/5xx senaryoları.
- [ ] Timeout, cancellation, response body limit testleri.
- **Çıktı:** Uçtan uca gerçek bir HTTP isteği gönderilebiliyor (henüz retry/signing yok).

### Faz 3 — Signing
- [ ] `IWebhookSigner` + `HmacSha256Signer` implementasyonu.
- [ ] Timestamp + body imzaya dahil edilir (replay koruması).
- [ ] Test: Aynı payload, farklı secret → farklı imza. Timestamp değişince imza değişir. `Verify` doğru/yanlış secret ile doğru sonuç döner.
- [ ] **Kritik test:** Clock skew toleransı — imza üretilirken kullanılan timestamp ile doğrulama arasında X dakikadan fazla fark varsa reddedilmeli.
- **Çıktı:** Gönderilen her istek `Wiaoj-Signature` header'ı taşıyor, doğrulanabiliyor.

### Faz 4 — Retry Orchestration
- [ ] `IRetryPolicy` + `ExponentialBackoffRetryPolicy`.
- [ ] `RetrySchedulingMiddleware` — hata durumunda `IWebhookTransport.EnqueueAsync(job, delay)` ile yeniden kuyruklama.
- [ ] Test: `FakeTimeProvider` ile "N. deneme sonrası doğru gecikme hesaplanıyor mu?", "MaxAttempts aşılınca retry durmalı mı?"
- [ ] Jitter testleri: aynı input'la art arda çağrılan `GetNextDelay` deterministik olmayan ama sınırlar içinde sonuç vermeli.
- **Çıktı:** Başarısız teslimatlar otomatik olarak, backoff ile tekrar deneniyor.

### Faz 5 — Dead Lettering
- [ ] `Tyto.DeadLettering` entegrasyonu — `MaxAttempts` aşılınca job'ın dead letter'a düşmesi.
- [ ] Test: "Son deneme de başarısız olursa, dead letter store'a doğru sebep/açıklama ile kayıt düşüyor mu?"
- **Çıktı:** Kalıcı başarısızlıklar kaybolmuyor, izlenebilir hale geliyor.

### Faz 6 — Idempotency
- [ ] `IIdempotencyKeyGenerator` (varsayılan: event ID sabit, attempt numarası dahil değil).
- [ ] `IdempotencyMiddleware` — aynı key ile ikinci kez dispatch edilirse pipeline'ın erken kesilmesi (veya aynı handle'ın döndürülmesi).
- [ ] Test: Aynı event iki kez dispatch edilirse transport'a sadece bir kez ulaşmalı (concurrency-safe: iki paralel dispatch aynı anda gelirse race condition testi).
- **Çıktı:** Duplicate event'ler güvenle engelleniyor.

### Faz 7 — Distributed Rate Limiting
- [ ] `IWebhookRateLimiter` + `DistributedCounter` tabanlı implementasyon.
- [ ] `RateLimitMiddleware` — limit dolduğunda pipeline'ı durdurup job'ı gecikmeli olarak yeniden kuyruklama.
- [ ] Test: `IDistributedCounter` sahte implementasyonuyla "limit dolunca dispatch reddediliyor mu, doğru `RetryAfter` dönüyor mu?"
- [ ] Entegrasyon testi (gerçek Redis ile, `Testcontainers` kullanılarak): Çoklu instance simülasyonu, gerçekten distributed çalıştığının kanıtı.
- **Çıktı:** Hedef URL bazlı, instance sayısından bağımsız gerçek rate limit.

### Faz 8 — Delivery Log & Persistence
- [ ] `IWebhookDeliveryStore` + EF Core implementasyonu.
- [ ] Her attempt'in (status code, latency, response body özeti, hata mesajı) kaydı.
- [ ] Test: "N başarısız + 1 başarılı denemeden sonra history doğru sırada, doğru veriyle dönüyor mu?"
- **Çıktı:** Her webhook'un tam denem geçmişi sorgulanabilir.

### Faz 9 — Options & Runtime Konfigürasyon
- [ ] Tüm middleware'lerin `IOptionsMonitor<T>` kullanacak şekilde refactor edilmesi.
- [ ] Test: appsettings.json'dan okunan değerlerin doğru bind edildiği, runtime'da `IOptionsMonitor.OnChange` tetiklendiğinde davranışın güncellendiği.
- **Çıktı:** Restart gerektirmeden production ayarları değiştirilebiliyor.

### Faz 10 — Pipeline Genişletilebilirliği
- [ ] `IWebhookMiddleware` sözleşmesinin son hali, sıralamanın builder üzerinden tam kontrolü.
- [ ] Test: Özel bir middleware eklenip pipeline'a enjekte edildiğinde, doğru sırada çalıştığının doğrulanması.
- [ ] Negatif test: Bir middleware `next()` çağırmazsa pipeline'ın orada durması (short-circuit senaryosu).
- **Çıktı:** Kullanıcılar kendi cross-cutting concern'lerini (audit, custom header enjeksiyonu, PII masking) kütüphaneyi fork etmeden ekleyebiliyor.

### Faz 11 — Inbound (Gelen Webhook) Desteği
- [ ] `Wiaoj.Webhooks.Inbound.AspNetCore`: `MapWebhookReceiver<TVerifier>(path)`.
- [ ] `IInboundSignatureVerifier` sözleşmesi + generic HMAC doğrulayıcı.
- [ ] Test: Geçerli/geçersiz imza, eksik header, body tamperlanmış senaryoları.
- [ ] Raw body'nin middleware pipeline'ında bozulmadan (buffering) okunduğunun testi.
- **Çıktı:** Kendi API'nize gelen 3. parti webhook'ları güvenle karşılayabiliyorsunuz.

### Faz 12 — Sağlayıcı Adaptörleri
- [ ] `Wiaoj.Webhooks.Inbound.Providers.Stripe`, `.GitHub` — her biri kendi imza formatını `IInboundSignatureVerifier` üzerinden implemente eder.
- [ ] Test: Her sağlayıcının gerçek dokümantasyonundaki örnek payload + imza çiftleriyle doğrulama (fixture-based test).
- **Çıktı:** Yaygın sağlayıcılardan gelen webhook'lar sıfır ekstra kod ile doğrulanabiliyor.

### Faz 13 — Uçtan Uca Entegrasyon Testleri
- [ ] `Testcontainers` ile gerçek Postgres/RabbitMq/Redis üzerinde tam senaryo: dispatch → sign → rate limit → HTTP → retry → dead letter → delivery log.
- [ ] Chaos testleri: transport bağlantısı kesilirse, hedef sunucu yavaşsa/timeout atarsa davranış.
- **Çıktı:** Kütüphane "gerçek dünya" koşullarında kanıtlanmış oluyor.

### Faz 14 — Dokümantasyon & Örnek Projeler
- [ ] Her paket için ayrı README, `samples/` klasöründe minimal + gelişmiş örnek projeler.
- [ ] Migration rehberi: Polly'den, ham `HttpClient`'tan, elle yazılmış retry kodundan geçiş.
- **Çıktı:** Kütüphane dışarıdan da kolayca benimsenebilir hale geliyor.

---

## Bu Kütüphane Neyin Kapısını Açıyor?

Bu, `Wiaoj.Webhooks` bittiğinde elde edeceğiniz gerçek stratejik kazanımlar:

**1. `Wiaoj.RateLimit` kütüphanesi kendiliğinden doğar.**
Faz 7'de yazılan `IWebhookRateLimiter` + `DistributedCounter` entegrasyonu, webhook'tan bağımsız hale getirilip genelleştirilirse doğrudan bağımsız bir pakete dönüşür. Yani webhook'u bitirdiğinizde rate limit kütüphanesinin %80'i zaten elinizde olur — sıfırdan başlamak yerine extract etmiş olursunuz.

**2. `Wiaoj.Outbox` / Transactional Outbox pattern'i için temel atılmış olur.**
Faz 8'deki delivery log + Faz 4'teki retry scheduling, genel amaçlı bir "reliable message delivery" altyapısına genişletilebilir — sadece webhook'lar için değil, herhangi bir "en az bir kez, garanti teslim" gereken senaryo için (email gönderimi, 3. parti API senkronizasyonu vs.).

**3. `Prism.Delivery`'deki kanal dispatcher'ı (Discord, Email, WebPush) webhook motoru üzerine taşınabilir hale gelir.**
`DispatchToChannelAsync` şu an her kanal için elle yazılmış switch-case. Webhook motoru olgunlaştığında, "Discord'a gönderim" de aslında "imzalı, retry'li, rate-limitli bir outbound HTTP çağrısı" olarak modellenebilir — kod tekrarı azalır, tüm kanallar aynı gözlemlenebilirlik ve dayanıklılık garantilerine kavuşur.

**4. Müşterilere/3. partilere açık bir "Developer Platform" kapısı açılır.**
`Wiaoj.Webhooks.Inbound` + delivery log + retry UI (ileride) bir araya geldiğinde, Prism'in kendi müşterilerine "kendi event'lerinizi webhook olarak dinleyin" özelliği sunması mümkün hale gelir — Stripe, GitHub, Shopify gibi platformların sunduğu webhook deneyiminin aynısı, kendi ürününüzde.

**5. Test altyapısı (`Wiaoj.Webhooks.Testing`) diğer Tyto/Wiaoj paketlerine şablon olur.**
`InMemoryWebhookTransport`, `FakeTimeProvider` tabanlı deterministic retry testleri gibi pattern'ler, ileride yazılacak her yeni Wiaoj paketi için (RateLimit, Outbox, ne gelirse) "böyle test edilir" referansı haline gelir.

**6. Polly'ye bağımlılık tamamen ortadan kalkar.**
Retry, backoff, circuit-breaker mantığının hepsi kendi `IRetryPolicy` sözleşmenizde, kendi Tyto transport'unuza entegre, distributed-first olarak yaşar. Üçüncü parti bir resilience kütüphanesine muhtaç kalmazsınız — ve gelecekte "circuit breaker" gibi ek stratejiler de aynı sözleşme altında (`ICircuitBreakerPolicy`) doğal olarak eklenebilir.

**7. Açık kaynağa çıkarılabilir, ekosistemi tanıtan bir "flagship" paket olur.**
Tyto'nun ne kadar olgun olduğunu (transports, dead lettering, distributed counter, security) tek bir gerçek dünya use-case üzerinden gösteren en iyi referans implementasyon webhook kütüphanesi olur — potansiyel kullanıcılar/katkıcılar için en ikna edici giriş noktası.

---

## Lisans

MIT (öneri — netleştirilecek)

## Katkı

Her yeni middleware, signer, transport implementasyonu `Wiaoj.Webhooks.Testing` altındaki test double'ları kullanılarak TDD ile geliştirilmelidir. PR açmadan önce ilgili faz için yazılan testlerin (yukarıdaki roadmap) kırmızıdan yeşile geçtiğinin gösterilmesi beklenir.