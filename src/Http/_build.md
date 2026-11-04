# Wiaoj.Http

> HTTP header gramerini — ETag, Cache-Control, Link, Vary, Accept-Language, Deprecation, Sunset — elle string yazmaktan çıkaran değer tipleri ve ASP.NET Core filter'ları.

**Durum: tasarımdır, kod yoktur.** Bu klasörde `.csproj` yok, olacak da değil — aşağıdaki "paket ne zaman açılır" kapısı kapanana kadar tek ürün bu dosyadır. Almanac tarafında hangi header'ın hangi uçta döneceği zaten sözleşmeye bağlandı: [`docs/contracts/headers/baslik-matrisi.yaml`](file:///C:/Users/berta/source/repos/almanac/docs/contracts/headers/baslik-matrisi.yaml) ve onun `libraryGaps:` bölümü bu dosyanın gereksinim kaynağıdır; kararın gerekçesi [ADR-0005](file:///C:/Users/berta/source/repos/almanac/docs/decisions/0005-cache-etag-ve-kosullu-istek.md).

---

## Felsefe

1. **BCL'de olanı yeniden yazma.** `System.Net.Http.Headers` zaten `EntityTagHeaderValue`, `CacheControlHeaderValue`, `StringWithQualityHeaderValue` tiplerine sahip. Bu paketin varlık sebebi onları taklit etmek değil; BCL'de **hiç olmayan** şeyleri (Vary kümesi yönetimi, RFC 4647 lookup matching, Deprecation/Sunset) ve BCL'de olup **kullanılamayacak kadar hantal/eksik** olan şeyleri (span-based biçimlendirme, If-Match dayatması, `immutable`/`@`-timestamp desteği) kapatmak. Hangi tipin hangi kovaya düştüğü [BCL ile sınır](#bcl-ile-sınır) bölümünde tek tek yazılı.
2. **Header grameri, header politikası değil.** Çekirdek paket "bu değer RFC'ye uygun mu, nasıl yazılır/okunur" bilir. "Bu setin ETag'i hangi revision'dan üretilsin", "sunset ne zaman", "bu uç If-Match zorunlu mu" bilmez — bunlar tüketicinin kararlarıdır ve pakete **delegat/opsiyon olarak girer**, interface olarak değil.
3. **Çekirdek ASP.NET Core görmez.** `Wiaoj.Http` sadece BCL referansı taşır (Pagination çekirdeğinin mevcut kuralı). `HttpContext`, `Endpoint`, `Results` ile temas eden her şey `Wiaoj.Http.AspNetCore` satellite'ındadır. Domain/Application katmanı bir header tipini referans verebilir ama asla bir filter'ı görmez.
4. **Allocation profili korunur.** Devralacağımız `ETagGenerator` bugün `string.Create` + `stackalloc char[]` ile sıfır-heap ETag üretiyor [ETagGenerator.cs:54](file:///C:/Users/berta/source/repos/libraries/src/Pagination/Wiaoj.Pagination.AspNetCore/Caching/ETagGenerator.cs#L54). Bu karakter paketteki her `ToString()`/`TryFormat` için tabandır; "önce tip, sonra performans" diye bir faz yoktur.
5. **Re-export shim yok.** `ETagGenerator` taşındığında eski namespace'te `global using` veya typealias bırakılmaz. Sessiz geçiş, kırılmayı saklar; kıran geçiş, kırılmayı buldurur. İkinciyi seçiyoruz.

---

## İçindekiler

- [Nereye koyuyoruz — yerleşim kararı](#nereye-koyuyoruz--yerleşim-kararı)
- [Paket Mimarisi](#paket-mimarisi)
- [BCL ile sınır](#bcl-ile-sınır)
- [Çekirdek tipler](#çekirdek-tipler)
- [AspNetCore yüzeyi](#aspnetcore-yüzeyi)
- [Pagination'dan çıkarılma](#paginationdan-çıkarılma)
- [Almanac sınırı](#almanac-sınırı)
- [Paket ne zaman açılır](#paket-ne-zaman-açılır)
- [TDD Roadmap: A'dan Z'ye](#tdd-roadmap-adan-zye)
- [Bu kütüphane neyin kapısını açıyor](#bu-kütüphane-neyin-kapısını-açıyor)
- [Açık kararlar](#açık-kararlar)
- [Lisans · Katkı](#lisans--katkı)

---

## Nereye koyuyoruz — yerleşim kararı

Aday dört mevcut yer var; hiçbiri doğru değil. Gerekçeler dosya listelerine dayanıyor, tahmine değil.

| Aday | Gerçekte ne | Neden bu değil |
| --- | --- | --- |
| `Wiaoj.Net` | Tek proje, **giden** (outbound) taraflı: DNS, happy-eyeballs, SSRF politikası | Açı ters: `Wiaoj.Net` *istek yapan* taraf. Bu paket *cevap üreten* taraf. Aynı adda iki zıt yön, okuyanda sürekli "hangisi" sorusu doğurur. |
| `Wiaoj.Endpoints` | Tek proje, `IEndpoint` marker/keşif | Endpoint keşfi ile header grameri alakasız iki dert. Paket zaten küçük; içine HTTP değer tipleri koymak, keşif sözleşmesini tanımak zorunda bırakan bir yük getirir. |
| `Wiaoj.Primitives` | `src/Extensions/Primitives/Wiaoj.Primitives` — `Urn`, `SemVer`, `GuidV7`, `CronExpression`, `GeoCoordinate`, hash tipleri, `Base*String` ve her birinin `JsonConverters/` kardeşi | Desen doğru (BCL-only değer tipi + converter), kapsam yanlış. `Urn` ve `SemVer` bağımlılıksız ölü tipler; ETag/Cache-Control ise **ASP.NET Core karşılığıyla birlikte evrilmek zorunda** (filter, header yazımı, OpenAPI). Zorunlu ortak evrim gerektiren bir tipi, "asla değişmez" diye kurulan bir pakete koymak, o paketi değiştirilemez kılma sözüne aykırı. |
| **`Wiaoj.Http`** *(yeni)* | — | Repo konvansiyonuna (`X` + `X.AspNetCore` + `X.OpenApi` + `X.Testing`) uyuyor, yönü doğru, bağımlılık yönü tek yönlü. |

**Tek istisna — `LanguageTag`.** BCP 47 *kanonikleştirme* (parse, normalize, case/redundant-subtag düşürme) saf bir değer tipi; `Urn`/`SemVer` deseniyle birebir örtüşüyor ve `baslik-matrisi.yaml` da onu Primitives'e adresliyor. Karar: **kanonikleştirme Primitives'te, header işçiliği `Wiaoj.Http`'te.** `AcceptLanguageRange.Parse`, quality-value sıralaması ve RFC 4647 *lookup matching* `Wiaoj.Http`'te yaşar; `Wiaoj.Http`, `Wiaoj.Primitives`'e bağımlıdır, tersi olmaz.

---

## Paket Mimarisi

```
Wiaoj.Http
    └─ BCL-only. Değer tipleri + ayrıştırma/biçimlendirme:
       EntityTag, CacheControl, LinkValue, HttpDate, Vary, AcceptLanguageRange,
       DeprecationHeader, SunsetHeader, RelationType.
       Bağımlılık: Wiaoj.Primitives (LanguageTag için). FrameworkReference YOK.

Wiaoj.Http.AspNetCore
    └─ FrameworkReference Microsoft.AspNetCore.App.
       Endpoint filter'ları (ETag/Cache-Control/Vary/Link/Deprecation/Sunset yazımı,
       If-None-Match → 304, If-Match → 412/428), Results kısayolları, AddWiaojHttp(...).

Wiaoj.Http.OpenApi        (ileride, .AspNetCore landıktan sonra)
    └─ Filter'ların dokümana ne yazdığını üretir; başlıkları elle `OpenApiHeader`
       listesiyle tekrar tanımlama ihtiyacını kaldırır.

Wiaoj.Http.Testing        (Faz 5'te)
    └─ Deterministic zaman (FakeTimeProvider) + header okuma yardımcıları.
       Sunset/Deprecation testleri saate bağımlı; bunu çözmek paketin işi.
```

**`.Abstractions` yok, bilinçli.** RateLimiting'de `Abstractions` var çünkü *değiştirilebilir algoritma* var (`IRateLimitAlgorithm`). Burada takılacak bir strateji yok: gramer sabittir, politika ise tüketici delegat'ıyla gelir. Boş bir interface asamblesi, gelecekteki tek gerçek genişleme noktasını (politika delegat'ı) görünmez kılar. Çekirdekte `interface` görmeyeceksiniz.

**Bağımlılık yönü:** `Wiaoj.Pagination.AspNetCore` → `Wiaoj.Http.AspNetCore`. Pagination bir *tüketici*, sahip değil — RateLimiting'in webhook'la kurduğu ilişkinin aynısı.

---

## BCL ile sınır

Bu tablo paketin neden "var olanı sarmala" değil "eksik olanı yaz" olduğunu belirtir. Aşağıdaki beş satır derlemeden doğrulanamadı ve **Faz 0 doğrulama listesi**ne bağlıdır; orası netleşmeden tip yüzeyini kesinleştirmiyoruz.

| Konu | BCL'de ne var | Kova | Ne yazılır |
| --- | --- | --- | --- |
| ETag değeri | `EntityTagHeaderValue` (quoted/opaque, `IsWeak`, `Equals(other, useStrongComparison)`) | **sarmala** | BCL tipi doğru; `System.Net.Http` assembly'sinde durması ve `ToString()`'in tahmin edilemez allocation yapması ergonomiyi bozuyor. `EntityTag` struct'ı BCL semantiğini referans alır, span-based yazar. |
| Cache-Control | `CacheControlHeaderValue` (public/private, max-age, no-store, must-revalidate, s-maxage, stale-*) | **sarmala + tamamla** | `immutable` (RFC 8246) ve `@`-biçimli değerler yok mu — Faz 0. Sarmalaman sebebi allocation, semantiği çalmak değil. |
| Accept-Language | `StringWithQualityHeaderValue` (parse + quality) | **yok olan** | RFC 4647 *lookup matching* (basic/extended filtering) BCL'de hiç yok. Asıl boşluk burada. |
| Vary | Tip yok; alan `StringValues` olarak taşınıyor | **yok olan** | Mevcut listeye ekleme yaparken duplicate üretmeme, kanonik case, `*` ile diğer tüm alanların anlamsızlaşması. |
| Deprecation / Sunset | Tip yok | **yok olan** | RFC 9745 / RFC 8594 değerleri + ikisinin de önerdiği `Link: rel="deprecation"` / `rel="sunset"` eşleştirmesi. |
| HTTP-date | `"r"` = RFC 1123; `DateTimeOffset.TryParseExact` ile üç kabul edilen biçim denemesi | **sarmala** | Üç biçimi sırayla denemek + UTC'ye sabitlemek + 0-allocation yazım çağrının işi olmamalı. |

**Faz 0 doğrulama listesi** (kod yazmadan önce, referans assembly'den veya RFC metninden tek tek cevaplanır):

- [ ] `CacheControlHeaderValue` üzerinde `Immutable` var mı?
- [ ] `EntityTagHeaderValue.Parse` opaque-tag kuralını (`%x21 / %x23-7E / %x80-FF`) zorluyor mu, yoksa gevşek mi?
- [ ] `Microsoft.Net.Http.Headers` altında `DateFormatter` / `HttpDateParser` public API olarak erişilebilir mi, yoksa internal mi?
- [ ] RFC 9745 §2 gramaticinde `Deprecation` değeri kaç biçim kabul ediyor; `Sunset` için RFC 8594 §2.1 `@`-timestamp'a izin veriyor mu?
- [ ] RFC 8288 parametre uzantısı (`*`, `ext-target`) bugün hiç gerekmeyecek mi — gerekmiyorsa tipten çıkar mı?

---

## Çekirdek tipler

İmza taslağıdır, sözleşme değildir. Bir tipin RFC'si yoksa pakete girmiyor.

### `EntityTag` — RFC 9110 §8.8.3

```csharp
public readonly struct EntityTag : IEquatable<EntityTag>
{
    public bool IsWeak { get; }
    public ReadOnlySpan<char> OpaqueTag { get; }

    public static EntityTag Strong(string opaqueTag);
    public static EntityTag Weak(string opaqueTag);
    public static bool TryParse(ReadOnlySpan<char> headerValue, out EntityTag tag);

    public bool TryFormat(Span<char> destination, out int charsWritten);
    public bool Matches(EntityTag other, bool useStrongComparison = true);
}
```

- `W/"..."` ve `"..."` ayrımı tipte görünür: bugün `ETagGenerator` bunu düz `string` taşıyarak yapıyor [baslik-matrisi.yaml §libraryGaps → `ETag: gap (c)`](file:///C:/Users/berta/source/repos/almanac/docs/contracts/headers/baslik-matrisi.yaml).
- **Zayıf ETag ile strong karşılaştırma yapmaya çalışmak sessizce `false` dönmez, `InvalidOperationException` verir.** RFC 9110 §8.8.3 buna izin vermiyor ve bir cache katmanının bunu "eşleşmedi" diye yorumlaması, bayat içeriği taze sanmak demek.
- Almanac'ın revision-türevli değeri (`"r42.tr.code+label"`) için ayrı bir tip yok ve olması da gerekmiyor: `opaque-tag` kurallarına girdiği sürece (`"` ve kontrol karakteri yok) meşru bir opaque-tağ'dir. **Bu RFC'nin tanımladığı bir alt-yapı değildir** — semantiği üreten bize ait, grameri RFC'ye.
- İçerik-hash stratejisi (`GenerateWeakETag(ReadOnlySpan<byte>)`) pakette taşınmaya **devam eder**; ama strateji olarak tavsiye edilmez, çünkü bir ETag'in sabit kalması gereken şey response byte'ı değil temsildir. Mevcut davranışın yeri `Wiaoj.Http`'te test edilir, politikası Almanac'ta kalır.

### `CacheControl` — RFC 9111 + 5861 + 8246

```csharp
public sealed class CacheControl
{
    public static CacheControl Public(int maxAgeSeconds) { get; }
    public static CacheControl NoStore { get; }

    public int? MaxAge { get; init; }
    public int? SMaxAge { get; init; }
    public int? StaleWhileRevalidate { get; init; }   // RFC 5861
    public int? StaleIfError { get; init; }           // RFC 5861
    public bool MustRevalidate { get; init; }
    public bool Immutable { get; init; }              // RFC 8246 — ADR-0003 gereği v1'de HİÇ kullanılmaz, tipte durur

    public override string ToString();                // RFC 9111 önerilen sırasıyla
    public static bool TryParse(string headerValue, out CacheControl value);
}
```

- Sıralama sabit değil ama **deterministic**: aynı nesne her zaman aynı string'i üretir, yoksa ETag/Cache-Control ikilisi için yapılan "değişmedi mi" kontrolleri gürültü üretir.
- `NoStore` ile `MaxAge` birlikte geldiğinde `ToString()` **sessizce ikisini de yazmaz**; RFC 9111 §5.2.2.3 gereği `max-age` düşer. Bu kural test edilir, yoksa bir tüketici "no-store ama 5 dk cache'leniyor" tablosuyla karşılaşır.

### `LinkValue` / `LinkHeader` — RFC 8288 + 5829

```csharp
public sealed class LinkValue
{
    public required string Target { get; init; }
    public required string Rel { get; init; }
    public string? Anchor { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? Lang { get; init; }
}

public static class RelationType
{
    public const string Next = "next", Prev = "prev", First = "first", Last = "last";
    public const string LatestVersion = "latest-version";          // RFC 5829
    public const string PredecessorVersion = "predecessor-version"; // RFC 5829
    public const string SuccessorVersion = "successor-version";     // RFC 5829
    public const string Deprecation = "deprecation";                // RFC 9745
    public const string Sunset = "sunset";                          // RFC 8594
    public const string Alternate = "alternate";
}
```

- **Ayrıştırma tarafı ilk kez burada geliyor.** Bugünkü `Rfc8288LinkHeaderBuilder` yalnızca üretim yapıyor [Rfc8288LinkHeaderBuilder.cs:33](file:///C:/Users/berta/source/repos/libraries/src/Pagination/Wiaoj.Pagination.AspNetCore/Linking/Rfc8288LinkHeaderBuilder.cs#L33). Ayrıştırma olmadan bir tüketici `Link` başlığını okuyamaz; okuyamazsa `rel="deprecation"` servis listesini kimse takip edemez ve Deprecation header'ı süs olur.
- `rel` değeri çoklu olabilir (`rel="next prev"`); bugünkü builder bunu hiç desteklemiyor, tip destekliyor.

### `Vary` — RFC 9110 §12.5.5

```csharp
public sealed class VaryList
{
    public static VaryList Of(params string[] fields);
    public void Add(string field);          // duplicate yok, RFC 9110 kanonik case
    public bool IsWildcard { get; }         // "*" ise diğer her alan anlamsız
    public override string ToString();
}
```

Tek gerçek dert şu: iki ayrı filter aynı anda `Vary: Accept-Language` yazmaya çalıştığında ya duplicate ya da birbirini ezen tek değer çıkıyor. Bu tip, "üzerine ekle" semantiğini taşıyan tek sahipli bir küme.

### `AcceptLanguageRange` — RFC 9110 §12.5.4 + RFC 4647

```csharp
public sealed class AcceptLanguageRange
{
    public required LanguageTag Tag { get; init; }   // Wiaoj.Primitives'teki kanonik tip
    public double Quality { get; init; }             // varsayılan 1
}

public static class LanguageMatching
{
    public static LanguageTag? Lookup(IReadOnlyList<AcceptLanguageRange> request,
                                      IReadOnlyList<LanguageTag> available,
                                      MatchingMode mode = MatchingMode.Extended);
}
```

- **`Lookup` matching, `Filtering` matching değil.** Fark kullanıcıya görünür: `tr-TR` isteklerine `tr` etiketiyle cevap verebilmek için lookup gerekiyor; filtering (yıldız eşleştirme) bunu vermiyor. Mode parametresının varlık sebebi bu, "ileride genişler" değil.
- Kalite değeri eşitse istek sırası kazanır. Bu, BCL tipinin çözmediği bir ties-break kuralı.

### `DeprecationHeader` / `SunsetHeader` — RFC 9745 + 8594

```csharp
public readonly struct DeprecationHeader
{
    public DateTimeOffset When { get; }
    public string? ServiceLink { get; }     // rel="deprecation" hedefi
}

public readonly struct SunsetHeader
{
    public DateTimeOffset When { get; }
    public string? SuccessorLink { get; }   // rel="successor-version" hedefi
}
```

- Değerler `HttpDate` üzerinden yazılır/okunur; ayrı bir date tipi yoktur.
- **Paket hiçbir şeyi "kapatmaz"**: sunset tarihi geçtiğinde 410 dönmek, hangi setin sunset'e sokulacağı, migration penceresinin ne kadar olduğu — hepsi tüketici politikası. Pakette bir `IsExpired` kontrol noktası dahi olabilir ama filter bunu kendi karar vererek yapmaz.

---

## AspNetCore yüzeyi

### Filter'lar

| Filter | İşi | Yan etkisi |
| --- | --- | --- |
| `ConditionalGetEndpointFilter` | `If-None-Match` → **304** | Mevcut `ETagGenerator.IsNotModified` davranışının taşınmış hali |
| `PreconditionPutEndpointFilter` | `If-Match` yoksa → **428**, eşleşmiyorsa → **412** | RFC 6585 §4 + RFC 9110 §13.1 |
| `CachingEndpointFilter` | `Cache-Control` + `ETag` + `Vary` yazar | Endpoint metadata'sından okur |
| `LifecycleEndpointFilter` | `Deprecation` / `Sunset` / `Link` yazar | Alan bazlı, opsiyonel |

Ortak kural: **filter'lar değer üretmez, değeri bir delegat'tan alır.**

```csharp
// Politika pakette değil, tüketici de.
app.MapGet("/v1/lookup-sets/{setKey}", Handler)
   .WithConditionalGet(http => new ValueTask<EntityTag>(
        EntityTag.Strong(RevisionETag.For(http, ct))))   // ← Almanac'ın kendi fonksiyonu
   .WithCacheControl(CacheControl.Public(maxAgeSeconds: 60) with { StaleIfError = 3600 })
   .WithVary(VaryList.Of("Accept-Language", "Accept"));
```

Böylece `Wiaoj.Http.AspNetCore` "set revision'ı nedir" sorusunu hiç bilmez; Almanac'ın aggregate'ini, revision alanını, `publishedRevision` kavramını da bilmez. Bu satır [Almanac sınırı](#almanac-sınırı) bölümünün tamamını taşır.

### 412 ile 428 ayrımı

- İstemci `If-Match` **göndermedi** ve sunucu concurrency kontrolü **zorunlu** kıldıysa → **428 Precondition Required** (RFC 6585 §4).
- `If-Match` gönderdi ama eşleşmedi → **412 Precondition Failed** (RFC 9110 §13.1.2).
- `If-Match: *` ve kaynak var → eşleşme **başarılı**; `*` "içerik aynı mı" demek değil, "var mı" demek. Yanlış çevrilen bu kural, PATCH akışında haksız 412 üretir. Test edilecek ilk satır budur.
- **Zayıf ETag `If-Match` içinde gönderilirse RFC 9110 §13.1.3 gereği eşleşme sayılmaz** → 412. "Ama kullanıcı aynı değeri yapıştırdı" durumu gerçek; problem detail'in bunu ayırt eden bir `type` taşıması tüketicinin işi.

### `Results` kısayolları

`TypedResults.NotModified()` zaten var; eksik olan 412/428 ve header'lı 200. `Results` kısayolları bunu tek satıra indirir, ama **problem detail'i buradan üretmeyiz** — `Wiaoj.Results` ve Almanac'ın `problem-types.yaml`'ı zaten sahibi. Çakışma noktası Faz 3'te bilinçli olarak konuşulur.

### DI kayıt biçimi

[CONTRIBUTING.md](file:///C:/Users/berta/source/repos/libraries/CONTRIBUTING.md) kurallarına birebir uyulur; kod yazılmadan önce karara bağlandı:

- `AddWiaojHttp(this IServiceCollection)` → `IWiaojHttpBuilder` döner (fluent).
- `AddWiaojHttp(this IServiceCollection, Action<IWiaojHttpBuilder>)` → **`IServiceCollection`** döner, callback fluent olanın üstünde yazılır.
- `IWiaojHttpBuilder` public interface, kök namespace'te; `WiaojHttpBuilder` `internal sealed`, `Wiaoj.Http.DependencyInjection` altında.
- Kolaylıklar (`UseSunset(...)`, `DefaultCacheControl(...)`) `IWiaojHttpBuilder` extension metotlarıdır ve **builder'ın namespace'indedir**.
- `AddWiaojHttp` iki kez çağrılırsa güvenlidir (`TryAdd*`).

Aslında çekirdek pakette DI kaydı gerekip gerekmediği Faz 1'in gerçek kararlarından biri: değer tipleri stateless'se, `.AspNetCore` dışında bir builder hiç olmayabilir. Kuralı önceden yazmamın sebebi, o kararın sonra "nasıl olsa bir builder var" diye kaymaması.

---

## Pagination'dan çıkarılma

Taşınan iki tip ve **tam** çağrı listesi (ölçülmüştür, tahmin değildir):

| Simge | Şu an | Kullanıldığı yer |
| --- | --- | --- |
| `ETagGenerator` | [Caching/ETagGenerator.cs:21](file:///C:/Users/berta/source/repos/libraries/src/Pagination/Wiaoj.Pagination.AspNetCore/Caching/ETagGenerator.cs#L21) | `PaginationEndpointFilter.cs:78,81` + 12 test çağrısı |
| `Rfc8288LinkHeaderBuilder`, `Rfc8288Relations` | [Linking/Rfc8288LinkHeaderBuilder.cs:33](file:///C:/Users/berta/source/repos/libraries/src/Pagination/Wiaoj.Pagination.AspNetCore/Linking/Rfc8288LinkHeaderBuilder.cs#L33) | `PaginationEndpointFilter.cs:201,215` + 10 test çağrısı |

Dışarıdan (repo içi) tüketici: **yok**. Blast radius tek filter ve kendi testleri. Bu yüzden extraction ucuz; ama bu ölçüm sadece `libraries` reposu için geçerli — `verba`, `prism`, `arcanum` gibi diğer repolar bu paketleri NuGet üzerinden tüketiyorsa **public API kırılmasıdır** ve Faz 4'ün ilk adımında onlarda da grep gerekir. Bu dosya o taramayı yapmadı; yapmadan "etkisi yok" demiyoruz.

Sıra:

1. `Wiaoj.Http` + `Wiaoj.Http.AspNetCore` projeleri açılır; tipler **yeniden yazılmaz**, taşınır (git move ile, history korunur).
2. `ETagGenerator` → `EntityTag`/`ConditionalGet` yüzeyine sarılır; `static class` API'si `EntityTag` struct'ına dönüşür. Mevcut `FormatWeakETag(XxHash3)` overload'ı (önden hesaplanmış hash için) **olduğu gibi korunur** — yoksa streaming yol iki kez hash'ler.
3. `Rfc8288LinkHeaderBuilder.Build(metadata, uriFactory)` → `LinkHeader` koleksiyon üretimi; `PageMetadata`/`CursorMetadata` **`Wiaoj.Pagination`'de kalır**, `Wiaoj.Http` onları görmez. Bu yüzden builder'ın yerini alan şey `LinkValue` listesi + bir `PaginationLinkFactory` *adaptörüdür*, adaptör ise Pagination'da durur.
4. `PaginationEndpointFilter` güncellenir; `Wiaoj.Pagination.AspNetCore.csproj`'a `Wiaoj.Http.AspNetCore` referansı eklenir.
5. Testler taşınır (`ETagGeneratorTests.cs`, `Rfc8288LinkHeaderBuilderTests.cs`) ve **red-green kaydıyla**: taşımadan önce yeşil, sonra da yeşil — arada davranış değişmediğini kanıtlayan tek şey test isimlerinin birebir korunmasıdır.
6. [Wiaoj.Pagination.AspNetCore/README.md](file:///C:/Users/berta/source/repos/libraries/src/Pagination/Wiaoj.Pagination.AspNetCore/README.md) §4 "Standalone RFC 8288 Link Header Generation" ve §5 "Standalone ETag Generation & Verification" bölümleri kaldırılır, yerlerine tek cümle + link.
7. `src/Pagination/README.md` paket tablosundaki `.AspNetCore` satırından "RFC 8288 Link headers / XxHash3 ETag" ifadeleri çıkarılır — Pagination artık bu iki işin sahibi değildir.
8. **Re-export shim yok.** `Wiaoj.Pagination.AspNetCore.Caching` namespace'i silinir. Kullanan her yer derleme hatası verir ve bu, istenen sonuçtur.

Bu fazın **tek kabul ölçütü**: Pagination testleri taşınmadan önce ve sonra aynı sayıda ve aynı isimlerle geçiyor. Davranış farkı iddiası yoksa diff'te mantık satırı da olmamalı — olursa extraction değildir, rewrite'tır ve ayrı fazdır.

---

## Almanac sınırı

Pakete girmeyecek şeyler, girecekler kadar net yazılmalı; yoksa "genel amaçlı" adı altında servis mantığı pakete sızar.

| Kalır (Almanac) | Neden genel değil |
| --- | --- |
| Revision → ETag stratejisi (`"r{revision}.{lang}.{fields}"`) | Set revision kavramını sadece Almanac biliyor. [ADR-0005](file:///C:/Users/berta/source/repos/almanac/docs/decisions/0005-cache-etag-ve-kosullu-istek.md) |
| Uç bazında `Cache-Control` tablosu | Hangi uca hangi politika uygulanacağı, [baslik-matrisi.yaml](file:///C:/Users/berta/source/repos/almanac/docs/contracts/headers/baslik-matrisi.yaml) içinde bir sözleşme maddesidir; library kopyası olursa iki kaynak doğar. |
| Hangi setin sunset'e sokulacağı, migration penceresi | İş kararı. |
| Geçer `current revision` okuma + optimistic concurrency token | Aggregate davranışı. |
| 412/428'in **problem detail gövdesi** | Tip URN'leri Almanac'a ait (`problem-types.yaml`). |
| `Vary`'e hangi alanların gireceği (field listesinin **girmediği** kararı dahil) | Temsil hangi bileşenlere bağlı, domain bilgisi. |

Paketten alınan: `EntityTag`, `CacheControl`, `VaryList`, `LinkValue`/`RelationType`, `HttpDate`, `AcceptLanguageRange`/`LanguageMatching`, `DeprecationHeader`/`SunsetHeader`, `ConditionalGet`/`Precondition` filter'ları.

Bir satırlık test: **`Wiaoj.Http` içinde "set", "lookup", "revision", "almanac" kelimelerinden biri geçiyorsa tasarım yanlıştır.**

---

## Paket ne zaman açılır

Kod yazmak için kapı, bu dosyanın onaylanması değil. Kapı:

1. Almanac'ın **ilk okuma ucu** (`GET /v1/lookup-sets/{key}`) gerçek bir `WebApplicationFactory` testinde ETag üretip `If-None-Match` ile 304 alıyor — yani header yüzeyi bir kez **gerçek tüketici tarafından** denenmiş oluyor.
2. `baslik-matrisi.yaml`'daki 15 uç ↔ OpenAPI paritesi koruyor (şu an koruyor).
3. Almanac tarafında elle string yazılan her header satırı (`Headers.CacheControl = "public, max-age..."`) sayıldı ve kaçıncı tekrar olduğu biliniyor — repetition eşiği geçilmeden paket açılmıyor.

3. maddenin gerekçesi: `ETagGenerator` pagination dışına çıktığı gün, onu kullanan her endpoint filter'ı bundan sonra "bu paketin mantıklı bir yüzeye sahip olması" gerektiğini iddia eden bir örnek olur. Şu an elimizde tek bir gerçek tüketici var (Pagination). **Tekrar sayısı = 1 iken soyutlama, spekulyasyondur.** Paket açılış kararı, Almanac'ta ikinci bir kullanıcının fiilen doğmasıyla verilir.

Bu kapı kapanana kadar tek yazılı ürün bu dosyadır ve onun tek işi, kapı açıldığında kararları yeniden tartışmamaktır.

---

## TDD Roadmap: A'dan Z'ye

Her fazın çıktısı "derlenen kod" değil, **kanıtlanmış bir davranıştır**. [`.agents/rules/evidence-based-qa.md`](file:///C:/Users/berta/source/repos/libraries/.agents/rules/evidence-based-qa.md) gereği her faz red→green kaydı ve Claim-Evidence tablosu taşır.

### Faz 0 — Doğrulama, kod yok

- [ ] [BCL ile sınır](#bcl-ile-sınır) altındaki 5 maddelik liste cevaplanır.
- [ ] `EntityTag`'in BCL ile çakışma haritası çıkarılır: hangi overload'lar gerçekten fazla?
- [ ] Bu dosyada "yok" denen her iddia (Vary tipi, Deprecation tipi, RFC 4647 matching) referans assembly'den tekrar doğrulanır.
- **Çıktı:** Kesinleşmiş tip yüzeyi. Bu faz bitmeden Faz 1 başlamaz.

### Faz 1 — `EntityTag` + `HttpDate` (saf çekirdek)

- [ ] Strong/weak/opaque ayrımı, `Matches`, `TryParse`, `TryFormat`.
- [ ] Test: zayıf tag ile strong karşılaştırma **exception** atıyor; `If-Match: *` var-yok semantiği; kaçışlı `"` içeren opaque tag redediliyor.
- [ ] Test: IMF-fixdate round-trip; üç kabul edilen tarih biçiminin ayrışması; `ToString()` 0 allocation (Span assertion'ı ile).
- **Çıktı:** Bağımlılıksız, tek başına kullanılabilir iki tip.

### Faz 2 — `CacheControl` + `VaryList`

- [ ] RFC 9111 §5.2 direktif kümesi; 5861 `stale-*`; 8246 `immutable`.
- [ ] Test: `no-store` + `max-age` gelince `max-age` düşüyor; deterministic sıralama; `Vary` duplicate yok; `*` gelince diğer alan düşüyor.
- **Çıktı:** Elle string yazımı tamamen gereksiz hale gelir.

### Faz 3 — `LinkValue` + parser, `AcceptLanguageRange` + `LanguageMatching`

- [ ] RFC 8288 parse **ve** format; quoted/unquoted parametre, çoklu `rel`.
- [ ] `Lookup` matching basic vs extended.
- [ ] Test: `tr-TR,tr;q=0.8` isteğinde `tr` etiketi kazanıyor; kalite eşitliğinde sıra kazanıyor; `*` fallback; target URI'sinde `,` bulunca parse çöküyor mu (RFC 8288 quoted-string escape yolu var mı, Faz 0'a bağlı).
- **Çıktı:** Paket artık hem yazıyor hem okuyor.

### Faz 4 — `DeprecationHeader` + `SunsetHeader`

- [ ] RFC 9745 / 8594 değer biçimleri + ilişkili `Link` rel eşleştirmesi.
- [ ] Test: `@`-timestamp ve HTTP-date round-trip; sunset tarihi `Deprecation` tarihinden önceyse (uygunsuz sıra) uyarı/behavior dokümante.
- **Çıktı:** API lifecycle başlıkları tek yerden.

### Faz 5 — `Wiaoj.Http.AspNetCore`

- [ ] `ConditionalGetEndpointFilter` (304), `PreconditionPutEndpointFilter` (412/428), `CachingEndpointFilter`, `LifecycleEndpointFilter`.
- [ ] `IWiaojHttpBuilder` kayıt biçimi CONTRIBUTING'e göre.
- [ ] Test: 428 ↔ 412 ayrımı; `If-Match: *`; zayıf `If-Match` → 412; 304 gövde taşımıyor; `HEAD` yanıtında ETag var.
- [ ] `Wiaoj.Http.Testing`: `FakeTimeProvider` ile deterministic sunset.
- **Çıktı:** Almanac bu fazda elle header yazmayı bırakır.

### Faz 6 — Pagination extraction

- [ ] [Pagination'dan çıkarılma](#paginationdan-çıkarılma) bölümünün 8 adımı.
- [ ] Diğer repolarda (NuGet tüketicileri) grep: kaç yer kırılıyor, ölçülür.
- **Çıktı:** Pagination'da HTTP grameri kodu kalmamış.

### Faz 7 — `Wiaoj.Http.OpenApi`

- [ ] Filter'ların ürettiği header tanımlarını dokümana yaz; `Produces(...).WithHeaders()` tekrarı ortadan kalkar.
- [ ] Test: üretilen OpenAPI, [almanac-v1.yaml](file:///C:/Users/berta/source/repos/almanac/docs/contracts/openapi/almanac-v1.yaml) ile alan alan aynı (bu, el ile yapılan 15/15 parite kontrolünün kalıcı hali).
- **Çıktı:** Sözleşme ↔ davranma ayrışması makineyle kapatılır.

**Sıra kuralı:** Faz 6, Faz 5'ten önce gelmez. Önce yüzey sabitlenir, sonra mevcut kod oraya taşınır.

---

## Bu kütüphane neyin kapısını açıyor

1. **`Wiaoj.Pagination.AspNetCore` küçülür.** Filter'ın içinden iki statik sınıf çıkınca, Pagination'ın `.AspNetCore` paketi yalnızca "paging metadata → sonuç şekli" işini yapar. Bugün orada duran HTTP grameri, pagination'ı kullanmak istemeyen bir API için de gerekli; bu iki ihtiyacın aynı pakette olması, ikincisini ilkiyle rehin tutuyor.

2. **Header sözleşmesi tek kaynaktan üretilir.** [baslik-matrisi.yaml](file:///C:/Users/berta/source/repos/almanac/docs/contracts/headers/baslik-matrisi.yaml) ile kod arasındaki paralellik elle takip ediliyor; `Wiaoj.Http.OpenApi` ile makineye geçer. Sözleşme dokümanı ile davranışın ayrıştığı her API'de, ayrışma testte değil üretimde görünür.

3. **Koşullu istek, "özel bir şey yapmak" olmaktan çıkar.** If-Match dayatması bugün bir repoda bile yok; bir endpoint'in 428/412 davranışını edinme maliyeti tek satıra iner. Bu, optimistic concurrency'yi domain katmanında tekrar tekrar yazma alışkanlığını bitiren şey.

4. **Lifecycle başlıkları ilk kez gerçek olur.** `Deprecation` ve `Sunset` yazmak tek başına bir şey değiştirmiyor; `Link: rel="deprecation"` ve parser olmadan hiçbir istemci takip edemiyor. Paket ikisini birlikte verdiğinde, "kullanımdan kaldırma" bir duyuru değil takip edilebilir bir sözleşme olur — [B.4 legacy göçü](file:///C:/Users/berta/source/repos/almanac/docs/ALMANAC-CATALOG.md) için gereken mekanizma bu.

---

## Açık kararlar

1. **`LanguageTag` gerçekten Primitives'te mi?** Kanonikleştirme oraya, `LanguageMatching` buraya — bu dosyanın önerisi. Karşı argüman: Primitives'e "dil" kavramını sokmak, onun "kimlik + hash + zaman" kapsamını genişletiyor. Cevap, Primitives'te `GeoCoordinate`'ün zaten durması (alan-kapsam "coğrafya" da genişletilmişti). Faz 0'da kapanır.
2. **`EntityTag` mi `ETag` mi?** C# tip adı olarak `ETag` okunabilirliği kazanıyor ama `Etag`/`ETag` yazım ikilemi repo genelinde temiz değil. `EntityTag` RFC terminolojisiyle birebir; eğilim bu yönde.
3. **`CacheControl` mutable mı immutable mı?** Yukarıdaki taslak `sealed class` + `init` karışımı; `with` expression'ı struct'ta çalışmıyor. Faz 2'den önce yüzey netleşmeli, çünkü `.AspNetCore` extension metotları bu kararın üstüne yazılıyor.
4. **Parser'ın agresifliği.** RFC'ye aykırı bir `Link` başlığına 0-allocation fallback mi, `false` mu, exception mı? Header'ı **gönderen** tarafta exception doğru; **alan** tarafta exception bir istemcinin bozuk başlığının 500 üretmesi demek. Yanıt: ayrı iki yüzey, tek tip.
5. **`Wiaoj.Http` in açık kaynak hedefi var mı?** RateLimiting ve Webhooks "bağımsız benimsenebilir utility" hedefiyle yazıldı. Bu paket için aynı iddia henüz yok — Almanac dışı ikinci tüketici Fiilen doğmadan evet denmez; bkz. [paket ne zaman açılır](#paket-ne-zaman-açılır).

---

## Lisans

MIT (repo geneliyle aynı varsayım).

## Katkı

- Her tip için RFC maddesi referans verilir (`§` numaralı); RFC'si olmayan davranış pakete girmez.
- Header **üreten** yolda hatalı değer exception, header **okuyan** yolda `false` — bu ayrım bozulursa bir istemcinin bozuk başlığı sunucu hatasına dönüşür.
- Testler `Wiaoj.Http.Testing`'in `FakeTimeProvider`'ı üzerinde yazılır; `DateTime.UtcNow` okuyan tek bir test bile kalmaz (Deprecation/Sunset zaten saat bağımlı).
- Faz 6 (extraction) hariç hiçbir PR mevcut bir paketin public API'sini kırmaz; kırarsa Claim-Evidence tablosunda kırılan tüketici listesi yer alır.
