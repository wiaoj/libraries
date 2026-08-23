# Wiaoj.RateLimiting

> Distributed, algoritma-agnostik, webhook'tan bağımsız rate limiting altyapısı — .NET için.

`Wiaoj.Webhooks.DistributedCounter` içinde doğdu; rate-limit kararı (`limit`, `IsAllowed`, `RetryAfter`) sayaç primitive'inin (`Wiaoj.DistributedCounter`) içine sızmıştı. Bu paket, o kararı sayaçtan ayırıp kendi başına yaşayabilen, webhook'a hiç bağımlı olmayan bir katman olarak çıkarır.

---

## Felsefe

1. **Sayaç (`IDistributedCounter`) rate limit'i bilmez.** "Artan sayı + TTL" primitive'i saf kalır; "limit aşıldı mı" kararını her zaman bir `IRateLimitAlgorithm` verir. Bu ayrım olmadan sayaç, her yeni kullanım senaryosunda (circuit breaker, quota, concurrency) rate-limit'e özel varsayımlar taşımaya başlar.
2. **Algoritma değiştirilebilir, primitive değil.** Fixed window, sliding window, token bucket — hepsi aynı `IRateLimitAlgorithm` sözleşmesi arkasında yaşar. Hangi algoritmanın hangi primitive'e ihtiyaç duyduğu (`IDistributedCounter` mı, ayrı bir token store mu) implementasyon detayıdır, tüketiciyi ilgilendirmez.
3. **Webhook, bu paketin bir tüketicisidir — sahibi değil.** `Wiaoj.Webhooks.RateLimiting` ileride ince bir adaptör paketine dönüşür (`WebhookRateLimiterAdapter : IWebhookRateLimiter`, `IRateLimitAlgorithm`'a delege eder). Aynı adaptör deseni; login brute-force koruması, API gateway, herhangi bir yerde tekrarlanabilir.
4. **İsim, davranışla birebir örtüşür.** "Sliding window" diye adlandırılan şey gerçekten kayan pencere olmalı — fixed window'a yanlışlıkla bu ismi vermek, kodu okuyan birinin olmayan bir burst koruması varsaymasına yol açar.

---

## İçindekiler

- [Paket Mimarisi](#paket-mimarisi)
- [Hızlı Başlangıç](#hızlı-başlangıç)
- [Çekirdek Soyutlamalar](#çekirdek-soyutlamalar)
- [Algoritmalar ve Primitive İhtiyaçları](#algoritmalar-ve-primitive-i̇htiyaçları)
- [Genişletme Noktaları](#genişletme-noktaları)
- [Wiaoj.Webhooks ile İlişki](#wiaojwebhooks-ile-i̇lişki)
- [TDD Roadmap: A'dan Z'ye](#tdd-roadmap-adan-zye)
- [Bu Kütüphane Neyin Kapısını Açıyor?](#bu-kütüphane-neyin-kapısını-açıyor)

---

## Paket Mimarisi

```
Wiaoj.RateLimiting.Abstractions
    └─ Sıfır bağımlılık. IRateLimitAlgorithm, RateLimitDecision, IRateLimitKeyStore gibi sözleşmeler.

Wiaoj.RateLimiting
    └─ Core: FixedWindowRateLimiter, SlidingWindowRateLimiter (weighted-window tekniğiyle).
       Wiaoj.DistributedCounter üzerine kurulu. Builder/DI: AddWiajRateLimiting(...).

Wiaoj.RateLimiting.TokenBucket
    └─ Token bucket, "mevcut token + son refill zamanı" gibi atomik çift-değer güncelleme
       ister — düz IDistributedCounter yetmez. Lua script tabanlı Redis implementasyonu
       (veya eşdeğeri) burada, ayrı ve opsiyonel bir pakette yaşar.

Wiaoj.RateLimiting.AspNetCore
    └─ HttpContext → key extraction → 429 + Retry-After middleware'i.
       System.Threading.RateLimiting ile nasıl bir arada duracağı burada netleşir
       (muhtemelen kendi PartitionedRateLimiter adaptörümüz).

Wiaoj.RateLimiting.Testing
    └─ InMemoryRateLimitAlgorithm, FakeTimeProvider tabanlı deterministic testler.
```

**Kural:** `Wiaoj.RateLimiting` çekirdek paketi hiçbir HTTP/ASP.NET Core referansı taşımaz. Web'e özgü her şey `.AspNetCore` satellite paketindedir. Çekirdek sadece "key → izin var mı" sorusuna cevap verir.

---

## Hızlı Başlangıç

```csharp
builder.Services.AddWiajRateLimiting(rl =>
{
    rl.UseFixedWindow(limit: 50, window: TimeSpan.FromSeconds(1));
});
```

```csharp
RateLimitDecision decision = await limiter.TryAcquireAsync("endpoint:123", cost: 1, ct);

if (!decision.IsAllowed)
{
    // decision.RetryAfter, decision.Remaining kullanılabilir
}
```

Tek bağımlılık `IRateLimitAlgorithm`. Hiçbir tüketici `IDistributedCounter`'ı doğrudan görmez.

---

## Çekirdek Soyutlamalar

### Algoritma — tek giriş noktası

```csharp
public interface IRateLimitAlgorithm
{
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default);
}
```

`cost` baştan var — bazı işlemler "1 birim" değil "5 birim" tüketir (ör. bulk endpoint). Sonradan eklemek breaking change olurdu.

### Karar — sonuç tip güvenli

```csharp
public readonly record struct RateLimitDecision(bool IsAllowed, TimeSpan? RetryAfter, long? Remaining = null);
```

### Key üretimi — pluggable

```csharp
public interface IRateLimitKeySelector<in TContext>
{
    string GetKey(TContext context);
}
```

Webhook, ASP.NET Core, ya da herhangi bir tüketici kendi `TContext`'ine göre bu sözleşmeyi implemente eder; çekirdek paket hangi context'in ne olduğunu bilmez.

---

## Algoritmalar ve Primitive İhtiyaçları

| Algoritma | Primitive | Not |
| --- | --- | --- |
| Fixed Window | `IDistributedCounter` (tek sayı + TTL) | Basit, ucuz. Pencere sınırında burst mümkün — düşük/orta hassasiyetli limitler için yeterli. |
| Sliding Window (weighted) | İki komşu `IDistributedCounter` penceresi | Önceki + mevcut pencerenin ağırlıklı ortalaması. Tam doğru değil ama pratikte burst'ü büyük oranda keser (Cloudflare'in kullandığı teknik). |
| Sliding Window (log) | Sorted set (`ZADD`/`ZREMRANGEBYSCORE`) | Tam doğru sliding window ama bellek/maliyet daha yüksek — yüksek hassasiyet gereken senaryolar için. |
| Token Bucket | Atomik çift-değer store (token count + son refill) | `IDistributedCounter` tek başına yetmez; ayrı paket (`Wiaoj.RateLimiting.TokenBucket`). |

**Kritik test (her algoritma için ortak):** Aynı key'e art arda gelen isteklerde, limit tam sınırda (`current == limit`) doğru davranıyor mu (off-by-one yok)? Pencere/refill sıfırlandığında yarım kalan state temiz mi?

---

## Genişletme Noktaları

| Ne değiştirmek istiyorsun? | Hangi interface'i implemente et |
| --- | --- |
| Rate limit algoritmasını | `IRateLimitAlgorithm` |
| Key üretimini (context'e özel) | `IRateLimitKeySelector<TContext>` |
| Karar sonrası davranışı (log, metrik) | `IRateLimitDecisionObserver` |
| Depolama backend'ini (Redis dışı) | Algoritmanın kendi bağımlı olduğu primitive'i (`IDistributedCounter` implementasyonu) |

Hiçbir yerde somut bir teknoloji (`RedisRateLimiter` gibi) çekirdek pakette hardcode edilmez.

---

## Wiaoj.Webhooks ile İlişki

`Wiaoj.Webhooks.RateLimiting` (eski adıyla `Wiaoj.Webhooks.DistributedCounter`), bu paketin ince bir tüketicisine dönüşür:

```csharp
internal sealed class WebhookRateLimiterAdapter : IWebhookRateLimiter
{
    private readonly IRateLimitAlgorithm _algorithm;
    private readonly DistributedRateLimitingOptions _options;

    public ValueTask<RateLimitResult> TryAcquireAsync(WebhookDeliveryContext context, CancellationToken ct = default)
    {
        string key = this._options.KeySelector(context);
        // IRateLimitAlgorithm.TryAcquireAsync sonucunu RateLimitResult'a map eder — başka mantık yok.
    }
}
```

`Wiaoj.Webhooks` artık `Wiaoj.DistributedCounter`'a değil, `Wiaoj.RateLimiting`'e bağımlıdır. `DistributedRateLimitingMiddleware` değişmez, sadece arkasındaki bağımlılık `IDistributedCounterFactory`'den `IRateLimitAlgorithm`'a kayar.

---

## TDD Roadmap: A'dan Z'ye

### Faz 0 — Abstractions İskeleti

- [ ] `IRateLimitAlgorithm`, `RateLimitDecision`, `IRateLimitKeySelector<T>` tanımları.
- [ ] `Wiaoj.RateLimiting.Testing`: `InMemoryRateLimitAlgorithm`, `FakeTimeProvider`.
- **Çıktı:** Derlenen ama çalışmayan iskelet.

### Faz 1 — Fixed Window

- [ ] `FixedWindowRateLimiter : IRateLimitAlgorithm`, `IDistributedCounter` üzerine kurulu.
- [ ] Test: Limit sınırında off-by-one yok. TTL dolunca sayaç sıfırlanıyor. Farklı key'ler birbirini etkilemiyor.
- [ ] Pencere sınırı burst testi — bilinen davranış olarak dokümante edilir, "bug" değil.
- **Çıktı:** `rl.UseFixedWindow(...)` çalışıyor.

### Faz 2 — Sliding Window (Weighted)

- [ ] İki komşu fixed window'un ağırlıklı ortalaması ile yaklaşık sliding window.
- [ ] Test: `FakeTimeProvider` ile pencere geçişlerinde ağırlığın doğru hesaplandığı, burst'ün fixed window'a göre gerçekten azaldığı.
- **Çıktı:** `rl.UseSlidingWindow(...)` çalışıyor, fixed window'dan ölçülebilir şekilde daha sıkı.

### Faz 3 — ASP.NET Core Entegrasyonu

- [ ] `Wiaoj.RateLimiting.AspNetCore`: middleware, `IRateLimitKeySelector<HttpContext>` varsayılan implementasyonu (IP/kullanıcı bazlı).
- [ ] `System.Threading.RateLimiting` ile ilişki netleştirilir (paralel mi çalışır, onun yerine mi geçer).
- [ ] Test: 429 + `Retry-After` header'ının doğru döndüğü, limit altında isteklerin etkilenmediği.
- **Çıktı:** Herhangi bir ASP.NET Core uygulamasına tek satırla rate limit eklenebiliyor.

### Faz 4 — Token Bucket

- [ ] `Wiaoj.RateLimiting.TokenBucket`: atomik refill + tüketim (Lua script veya eşdeğeri).
- [ ] Test: Burst'e izin verip sürekli yüksek hızda isteği reddettiği — fixed/sliding window'dan farkı kanıtlanır.
- **Çıktı:** Burst-toleranslı ama sürdürülebilir limit isteyen senaryolar için üçüncü bir seçenek.

### Faz 5 — Wiaoj.Webhooks Entegrasyonu (Geriye Dönük)

- [ ] `Wiaoj.Webhooks.DistributedCounter` → `Wiaoj.Webhooks.RateLimiting` olarak yeniden adlandırılır.
- [ ] `WebhookRateLimiterAdapter` yazılır, `DistributedRateLimitingMiddleware` buna bağlanır.
- [ ] Regresyon testi: Webhook tarafının mevcut davranışı (aynı limit/window semantiği) birebir korunuyor mu.
- **Çıktı:** Webhook modülü artık genel amaçlı rate limiting kütüphanesinin bir tüketicisi.

### Faz 6 — Gözlemlenebilirlik

- [ ] `IRateLimitDecisionObserver` — her karar (allow/deny) için opsiyonel hook (metrik, log).
- [ ] Test: Observer eklendiğinde her `TryAcquireAsync` çağrısında tetiklendiği, eklenmediğinde ekstra maliyet olmadığı (no-op path).
- **Çıktı:** Prometheus/OpenTelemetry gibi bir metrik sistemine kütüphaneyi fork etmeden bağlanılabiliyor.

### Faz 7 — Dokümantasyon & Örnekler

- [ ] Her paket için README, `samples/` altında minimal + ASP.NET Core örnek projesi.
- [ ] Migration rehberi: `Wiaoj.Webhooks.DistributedCounter`'dan bu pakete geçiş.
- **Çıktı:** Kütüphane webhook geçmişinden bağımsız, kendi başına benimsenebilir.

---

## Bu Kütüphane Neyin Kapısını Açıyor?

**1. `Wiaoj.Webhooks` sadeleşir.**
Rate-limit mantığı webhook'tan tamamen ayrılır; `Wiaoj.Webhooks.RateLimiting` birkaç satırlık bir adaptöre iner.

**2. Diğer Wiaoj/Tyto/Prism yüzeylerinde tekrar kullanılabilir.**
Login brute-force koruması, API gateway, `Prism.Delivery`'deki kanal dispatcher'ları (Discord/Email/WebPush) — hepsi aynı `IRateLimitAlgorithm`'ı, kendi `IRateLimitKeySelector<TContext>`'iyle tüketebilir.

**3. `Wiaoj.DistributedCounter` yeniden saflaşır.**
Rate-limit'e özgü varsayımlar (`limit`, `IsAllowed`) sayaçtan çıkınca, `IDistributedCounter` circuit breaker, quota, concurrency sayacı gibi tamamen farklı senaryolarda da değişmeden kullanılabilir hale gelir.

**4. Açık kaynağa çıkarılabilir, bağımsız bir "utility" paket olur.**
Webhook geçmişinden habersiz, sadece "dağıtık rate limiting" problemini çözen bir paket olarak; potansiyel kullanıcılar için Polly'nin rate-limiting eşdeğerlerinden (veya `System.Threading.RateLimiting`'in distributed olmayan halinden) daha iddialı bir alternatif.

---

## Lisans

MIT (öneri — netleştirilecek)

## Katkı

Her yeni algoritma implementasyonu `Wiaoj.RateLimiting.Testing` altındaki test double'ları kullanılarak TDD ile geliştirilmelidir. PR açmadan önce ilgili faz için yazılan testlerin kırmızıdan yeşile geçtiğinin gösterilmesi beklenir.
