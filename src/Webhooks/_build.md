# Wiaoj.Webhooks

Bu belge, `Wiaoj.Webhooks` motorunda tespit edilen 7 kritik mimari eksiklik ve üretim ortamı açığı için detaylandırılmış teknik geliştirme spesifikasyonudur. Her madde bağımsız bir GitHub Issue / Task olarak parçalanabilecek formatta hazırlanmıştır.

---

## Issue 1: Fix Orphaned `Retrying` Jobs on Node Crash / Restart in Stale Recovery

### 1.1. Problem Tanımı
Bir webhook teslimatı geçici bir hata aldığında (`WebhookDeliveryResult.TransientFailure`), `WebhookJobHandler` ve `RetryMiddleware` işin durumunu veritabanında `WebhookJobStatus.Retrying` olarak günceller ve gecikme süresi ile `IWebhookTransport.EnqueueAsync(job, delay)` çağrılır. 

Ancak, `InMemoryDelayedScheduler` içindeki gecikme süresi (örn. 5-30 dakika) işlerken sunucu pod'u yeniden başlarsa (OOM kill, deployment, crash) RAM'deki zamanlayıcı uçar. Sunucu tekrar ayağa kalktığında `StaleJobRecoveryService` ve `IWebhookStore.GetStaleJobsAsync` **yalnızca** `InFlight` ve `Queued` durumlarına bakar. `Retrying` durumundaki işler sorgulanmadığı için bu işler veritabanında sonsuza kadar `Retrying` durumunda yetim (orphan) kalır.

### 1.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Abstractions/IWebhookStore.cs`
- `Wiaoj.Webhooks/Internal/InMemoryWebhookStore.cs`
- `Wiaoj.Webhooks/Internal/StaleJobRecoveryService.cs`
- `Wiaoj.Webhooks/Recovery/WebhookRecoveryOptions.cs`

### 1.3. Teknik Gereksinimler & Değişiklikler
1. `WebhookJobRecord.NextAttemptAt` alanı `RetryMiddleware` veya `WebhookJobHandler` tarafından hesaplanan gecikme ile set edilmelidir (`now.Add(nextDelay)`).
2. `IWebhookStore.GetStaleJobsAsync` imzası veya implementasyonu, `Status == WebhookJobStatus.Retrying && NextAttemptAt <= now` olan işleri de kapsayacak şekilde güncellenmelidir.
3. `StaleJobRecoveryService`, vadesi geçmiş `Retrying` işlerini `Queued` durumuna çekip `IWebhookTransport`'a tekrar sokmalıdır.

```csharp
// IWebhookStore.cs sözleşme güncellemesi:
Task<IReadOnlyList<WebhookJobRecord>> GetStaleJobsAsync(
    DateTimeOffset? inFlightThreshold,
    DateTimeOffset? queuedThreshold,
    DateTimeOffset? retryingDueThreshold, // YENİ
    int maxCount,
    CancellationToken cancellationToken = default);
```

```csharp
// InMemoryWebhookStore.cs filtre güncellemesi:
bool isDueRetrying = retryingDueThreshold.HasValue
    && job.Status == WebhookJobStatus.Retrying
    && job.NextAttemptAt.HasValue
    && job.NextAttemptAt.Value <= retryingDueThreshold.Value
    && (!job.LockExpiresAt.HasValue || (inFlightThreshold.HasValue && job.LockExpiresAt.Value < inFlightThreshold.Value));
```

### 1.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] Bir iş `Retrying` durumuna geçerken `NextAttemptAt` alanı zorunlu olarak doldurulmalıdır.
- [ ] Gecikmeli kuyrukta beklerken transport çökerse, `StaleJobRecoveryService` süresi dolan `Retrying` işlerini başarıyla toplayıp kuyruğa tekrar basmalıdır.
- [ ] Süresi henüz dolmamış (`NextAttemptAt > now`) `Retrying` işleri recovery tarafından erken süpürülmemelidir.
- [ ] Unit & integration testleri: `FakeTimeProvider` ile zamanda ileri gidildiğinde `Retrying` işlerin transport'a yeniden ulaştığı doğrulanmalıdır.

---

## Issue 2: Bounded Capacity & Overflow Protection for `InMemoryDelayedScheduler`

### 2.1. Problem Tanımı
`Wiaoj.Webhooks.Transports.InMemory` altındaki `InMemoryDelayedScheduler`, arka planda bir `PriorityQueue<ScheduledJobItem, MonotonicTimestamp>` kullanır. Normal kanal için `Capacity` sınırlaması varken, gecikmeli zamanlayıcı için herhangi bir sınır yoktur (unbounded). Yüksek transient hata anlarında yüz binlerce retry işi RAM'de birikerek OOM (Out Of Memory) çökmesine yol açabilir.

### 2.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Transports.InMemory/InMemoryWebhookTransportOptions.cs`
- `Wiaoj.Webhooks.Transports.InMemory/Internal/InMemoryDelayedScheduler.cs`

### 2.3. Teknik Gereksinimler & Değişiklikler
1. `InMemoryWebhookTransportOptions` sınıfına `MaxDelayedCapacity` (varsayılan: 50.000) ve `DelayedOverflowPolicy` eklenmelidir.
2. Kapasite aşıldığında:
   - `DropOldest`: En ileri tarihteki işi düşürür.
   - `Reject`: İstisna fırlatır veya transport reddeder.
   - `PersistOnly`: RAM'e almaz, işi sadece DB'de `Retrying` olarak bırakır (Issue 1'deki recovery servisi zamanı gelince DB'den okur).

```csharp
public enum DelayedQueueOverflowPolicy {
    Reject = 0,
    DropOldest = 1,
    PersistOnlyFallback = 2
}

public sealed class InMemoryWebhookTransportOptions {
    // ...
    /// <summary>Gecikmeli kuyrukta tutulabilecek maksimum iş sayısı.</summary>
    public int MaxDelayedCapacity { get; set; } = 50_000;

    /// <summary>Gecikmeli kuyruk dolduğunda izlenecek strateji.</summary>
    public DelayedQueueOverflowPolicy DelayedOverflowPolicy { get; set; } = DelayedQueueOverflowPolicy.PersistOnlyFallback;
}
```

### 2.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] Gecikmeli kuyruk eleman sayısı `MaxDelayedCapacity` değerini aşamaz.
- [ ] `PersistOnlyFallback` modunda bellek dolduğunda sistem çökmez, iş bellekten atılsa bile DB'deki `NextAttemptAt` sayesinde kaybolmaz.
- [ ] Bellek baskısı durumunda diagnostic log üretilmelidir.

---

## Issue 3: Fair-Share Scheduling & Tenant Starvation Protection (Noisy Neighbor Defense)

### 3.1. Problem Tanımı
Mevcut `InMemoryWebhookTransport` ve `ShardedWebhookTransport`, FIFO sırasını korur ancak tenant/endpoint bazlı adil dağıtım (fair queueing) yapmaz. Eğer Tenant A tek seferde 1.000.000 webhook dispatch ederse, tüm kanallar ve worker havuzu Tenant A ile dolar. Tenant B'nin attığı tek bir acil `order.paid` eventi, Tenant A'nın yığınının arkasında saatlerce açlık (starvation) çeker.

### 3.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Transports.InMemory/ShardedWebhookTransport.cs`
- `Wiaoj.Webhooks.Transports.InMemory/InMemoryWebhookConsumer.cs`
- `Wiaoj.Webhooks.Transports.InMemory/InMemoryWebhookTransportOptions.cs`

### 3.3. Teknik Gereksinimler & Değişiklikler
1. Endpoint/Tenant bazlı akış kontrolü için **Deficit Round Robin (DRR)** veya **Weighted Fair Queueing (WFQ)** mantığı eklenmelidir.
2. `ShardedWebhookTransport`, hash partition yaparken aynı zamanda tek bir partition key'in shard kapasitesini domine etmesini sınırlandırmalıdır.
3. Worker havuzunun aynı partition key'i işlerken araya diğer partition key'lerden iş alabilmesi sağlanmalıdır (Fair Interleaving).

```csharp
public sealed class FairQueueingOptions {
    /// <summary>Tek bir endpoint/partition'ın aynı anda işlenebilecek maksimum concurrent worker kotası.</summary>
    public int MaxConcurrentExecutionsPerPartition { get; set; } = 2;

    /// <summary>Partition başına anlık tampon limiti.</summary>
    public int MaxBufferedJobsPerPartition { get; set; } = 1_000;
}
```

### 3.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] Tenant A 10.000 iş kuyrukladıktan sonra Tenant B 1 iş kuyrukladığında, Tenant B'nin işi Tenant A'nın tüm işleri bitmeden önce (en geç X ms içinde) işlenmelidir.
- [ ] Partition içi FIFO bozulmadan, partition'lar arası adil dağıtım sağlanmalıdır.

---

## Issue 4: Historical Data Retention, Pruning & Storage Management

### 4.1. Problem Tanımı
`IWebhookStore` üzerinde `SaveAsync` ve `RecordAttemptAsync` ile sürekli veri yazılır ancak eski/tamamlanmış işleri ve attempt loglarını temizleyen hiçbir sözleşme veya mekanizma yoktur. Yüksek throughput'lu ortamlarda `WebhookJobRecord` ve `Attempts` tabloları diski doldurur, index'leri yavaşlatır ve `GetStaleJobsAsync` sorgularının performansını düşürür.

### 4.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Abstractions/IWebhookStore.cs`
- `Wiaoj.Webhooks/Internal/InMemoryWebhookStore.cs`
- `Wiaoj.Webhooks/Internal/NullWebhookStore.cs`
- `Wiaoj.Webhooks/Retention/*` *(YENİ Dizin)*

### 4.3. Teknik Gereksinimler & Değişiklikler
1. `IWebhookStore` arabirimine temizleme metodu eklenmelidir:
   ```csharp
   Task<int> PruneJobsAsync(
       DateTimeOffset deliveredBefore,
       DateTimeOffset deadLetteredBefore,
       int batchSize,
       CancellationToken cancellationToken = default);
   ```
2. Arka planda periyodik olarak çalışan bir `WebhookRetentionCleanerService : BackgroundService` eklenmelidir.
3. Builder API'sine `UseRetentionPruning(...)` extension metodu eklenmelidir.

```csharp
public sealed class WebhookRetentionOptions {
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan DeliveredRetention { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan DeadLetterRetention { get; set; } = TimeSpan.FromDays(30);
    public int BatchSize { get; set; } = 1000;
}
```

```csharp
// Builder API Kullanımı:
webhooks.UseRetentionPruning(options => {
    options.DeliveredRetention = TimeSpan.FromDays(3);
    options.DeadLetterRetention = TimeSpan.FromDays(14);
});
```

### 4.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] `Delivered` olup saklama süresi dolan kayıtlar ve alt attempt history listeleri DB'den silinmelidir.
- [ ] `DeadLettered` kayıtlar kendi bağımsız saklama süresine göre temizlenmelidir.
- [ ] `Queued`, `InFlight`, `Retrying` durumundaki aktif işler ASLA silinmemelidir.
- [ ] Silme işlemi tek transaction'da DB'yi kilitlememek için `batchSize` parçalarıyla yürütülmelidir.

---

## Issue 5: Dual-Secret Signing for Zero-Downtime Outbound Secret Rotation

### 5.1. Problem Tanımı
Alıcı tarafında (`HmacWebhookSignerBase`), HTTP header'ındaki birden fazla `v1=` imzasını kontrol etme desteği mevcuttur (Rotation desteği). Ancak gönderici (sender) tarafında `WebhookEndpoint` yalnızca tek bir `Secret` tutar. Bir müşterinin anahtarı sızdığında veya periyodik değiştirildiğinde, anlık anahtar değişimi müşterinin alıcı tarafında `401 Unauthorized` hatalarına yol açar. Sıfır kesinti için göndericinin geçiş süresince hem eski hem yeni anahtarla imza basabilmesi gerekir.

### 5.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Abstractions/WebhookEndpoint.cs`
- `Wiaoj.Webhooks/WebhookEndpointBuilder.cs`
- `Wiaoj.Webhooks/Signing/SigningMiddleware.cs`
- `Wiaoj.Webhooks.Abstractions/IWebhookSigner.cs`

### 5.3. Teknik Gereksinimler & Değişiklikler
1. `WebhookEndpoint` modeline opsiyonel `SecondarySecret` eklenmelidir:
   ```csharp
   public sealed record WebhookEndpoint(
       WebhookEndpointId Id,
       Uri TargetUrl,
       EncryptedSecret<WebhookSigningContext> Secret,
       EncryptedSecret<WebhookSigningContext>? SecondarySecret = null, // YENİ
       IWebhookSigner? CustomSigner = null,
       IReadOnlyDictionary<string, string>? CustomHeaders = null);
   ```
2. `WebhookEndpointBuilder` sınıfına `.WithSecondarySecret(...)` desteği eklenmelidir.
3. `SigningMiddleware`, eğer endpoint üzerinde `SecondarySecret` tanımlıysa her iki anahtarla da imza üretmeli ve header değerini birleştirmelidir:
   ```http
   Webhook-Signature: t=1724190000,v1=primary_hash,v1=secondary_hash
   ```

### 5.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] `SecondarySecret` tanımlandığında, giden istekte aynı timestamp ile üretilmiş 2 adet `v1` (veya ilgili scheme) imzası yer almalıdır.
- [ ] Alıcı tarafı bu imzalardan herhangi biriyle doğrulamayı geçerse istek kabul edilmelidir.
- [ ] `SecondarySecret` null olduğunda performans kaybı veya fazladan imzalama maliyeti oluşmamalıdır.

---

## Issue 6: Endpoint Health Lifecycle & Persistent Auto-Quarantining

### 6.1. Problem Tanımı
`Wiaoj.Resilience` devre kesicisi (`CircuitBreakerMiddleware`) sadece **o anki process'in RAM'inde** yaşar. Bir hedef endpoint kalıcı olarak çökmüşse (404 Not Found, 410 Gone, DNS Unresolved), uygulama restart olduğunda devre kesici sıfırlanır ve sistem aynı ölü URL'e binlerce gereksiz HTTP isteği, DB kaydı ve retry yükü bindirmeye devam eder.

### 6.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Abstractions/WebhookEndpoint.cs`
- `Wiaoj.Webhooks.Abstractions/PermanentFailureReason.cs`
- `Wiaoj.Webhooks/Internal/WebhookDispatcher.cs`
- `Wiaoj.Webhooks/Health/*` *(YENİ Dizin)*

### 6.3. Teknik Gereksinimler & Değişiklikler
1. Endpoint için bir sağlık durumu enum'ı tanımlanmalıdır:
   ```csharp
   public enum WebhookEndpointStatus {
       Active = 0,
       Degraded = 1,
       Suspended = 2 // Karantinaya alınmış (Ölü)
   }
   ```
2. Arka arkaya N kez kalıcı hata (`PermanentFailure`) alan veya X gün boyunca hiç başarılı teslimat yapamayan endpoint'ler `Suspended` durumuna geçirilmelidir.
3. `WebhookDispatcher.DispatchAsync`, hedef endpoint `Suspended` ise ağa çıkmadan veya DB'ye job yazmadan isteği fast-reject etmeli (`WebhookEndpointSuspendedException`) veya doğrudan Dead-Letter'a kaydetmelidir.
4. `dispatcher.PingAsync(endpointId)` başarılı olursa endpoint tekrar `Active` durumuna dönebilmelidir.

### 6.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] Kalıcı olarak çöken endpoint'ler belirli eşik aşıldığında kalıcı olarak `Suspended` durumuna geçmelidir.
- [ ] `Suspended` endpoint'lere dispatch edilen işler gereksiz socket/transport trafiği yaratmamalıdır.
- [ ] Diagnostic log ve metrik üretilmelidir (`wiaoj.webhooks.endpoint.suspended.count`).

---

## Issue 7: Outbound HTTP Delivery Coalescing & Target Batch POST

### 7.1. Problem Tanımı
`IWebhookDispatcher.DispatchBatchAsync` ile 500 domain eventi tek bir veritabanı kaydı ve transport enqueue işlemiyle sisteme alınabilir. Ancak consumer katmanında bu 500 iş, hedef endpoint'e **500 ayrı tekil HTTP POST isteği** olarak gönderilir. Yüksek hacimli B2B entegrasyonlarında aynı hedefe saniyede yüzlerce tekil HTTP bağlantısı açmak socket tükenmesine ve hedef sunucuda rate-limit aşımına neden olur.

### 7.2. Etkilenen Dosyalar
- `Wiaoj.Webhooks.Abstractions/IWebhookDeliverer.cs`
- `Wiaoj.Webhooks/Internal/HttpWebhookDeliverer.cs`
- `Wiaoj.Webhooks/Internal/BatchHttpWebhookDeliverer.cs` *(YENİ Sınıf)*
- `Wiaoj.Webhooks/DependencyInjection/WebhookBuilderExtensions.cs`

### 7.3. Teknik Gereksinimler & Değişiklikler
1. Birden fazla payload'ı tek bir JSON array gövdesinde (`[ {event1}, {event2} ]`) toplayıp tek HTTP POST ile ileten `BatchHttpWebhookDeliverer` eklenmelidir.
2. N adet payload'ın tek istekte gitmesi durumunda imzalama algoritması toplu gövde üzerinden tek imza üretmelidir.
3. Yanıt HTTP 200 dönerse gruptaki tüm işler `Delivered` olarak işaretlenmeli, HTTP 5xx dönerse tüm grup retry'a alınmalıdır.
4. Builder üzerinden konfigüre edilebilmelidir:
   ```csharp
   webhooks.UseBatchHttpDelivery(options => {
       options.MaxBatchSize = 50;
       options.LingerTimeout = TimeSpan.FromMilliseconds(100);
   });
   ```

### 7.4. Kabul Kriterleri (Acceptance Criteria)
- [ ] Aynı hedefe yönelik kuyrukta bekleyen işler belirlenen `MaxBatchSize` ve `LingerTimeout` limitlerine göre tek HTTP POST altında birleştirilmelidir.
- [ ] Bireysel endpoint bazında toplu gönderim desteği açılıp kapatılabilmelidir (Geriye dönük uyumluluk).
- [ ] Tekil batch gönderiminde oluşan ağ hatası, batch içerisindeki tüm job attempt kayıtlarına doğru şekilde yansıtılmalıdır.

---

## Öncelik ve Yol Haritası Önerisi

| Öncelik | Issue | Karmaşıklık | Gerekçe |
|:---:|---|:---:|---|
| 🚨 **P0** | **Issue 1: Retrying Stale Recovery** | Düşük | Veri kaybı / yetim kayıt yaratan doğrudan bir tutarlılık bug'ıdır. |
| 🚨 **P0** | **Issue 2: Delayed Scheduler Memory Limit** | Orta | Yüksek retry anında OOM crash riskini ortadan kaldırır. |
| ⚠️ **P1** | **Issue 4: Data Retention & Pruner** | Orta | Veritabanının kontrolsüz büyümesini engeller. |
| ⚠️ **P1** | **Issue 5: Dual-Secret Rotation (Sender)** | Düşük | Güvenlik operasyonlarında sıfır kesinti sağlar. |
| 💡 **P2** | **Issue 6: Endpoint Auto-Quarantine** | Orta | Ölü endpoint'lerin sistemi meşgul etmesini önler. |
| 💡 **P2** | **Issue 3: Fair-Share Scheduling** | Yüksek | Multi-tenant mimaride tenant açlığını önler. |
| 💡 **P3** | **Issue 7: HTTP Batch Delivery** | Yüksek | Yüksek throughput'lu uç senaryolarda ağ verimliliği sağlar. |