using System.Text.Json;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Cryptography.Hashing;
using Wiaoj.Primitives.Snowflake;
using Wiaoj.Samples.Primitives;

// ─────────────────────────────────────────────────────────────────────────────
// Wiaoj.Primitives — Kapsamlı Kullanım Örnekleri
// ─────────────────────────────────────────────────────────────────────────────

JsonSerializerOptions jsonOpts = new() { WriteIndented = true };

PrintHeader("NON EMPTY STRING");
RunNonEmptyStringExamples();

PrintHeader("POSITIVE INT & NON NEGATIVE INT");
RunConstrainedIntExamples();

PrintHeader("BOUNDED STRING");
RunBoundedStringExamples();

PrintHeader("INTERVAL<T>");
RunIntervalExamples();

PrintHeader("MIME TYPE");
RunMimeTypeExamples();

PrintHeader("GUID V7");
RunGuidV7Examples();

PrintHeader("UNIX TIMESTAMP");
RunUnixTimestampExamples();

PrintHeader("SNOWFLAKE ID");
RunSnowflakeIdExamples();

PrintHeader("HEX STRING");
RunHexStringExamples();

PrintHeader("BASE64 STRING");
RunBase64StringExamples();

PrintHeader("BASE64 URL STRING");
RunBase64UrlStringExamples();

PrintHeader("BASE32 STRING");
RunBase32StringExamples();

PrintHeader("BASE62 STRING");
RunBase62StringExamples();

PrintHeader("SHA256 HASH");
RunSha256HashExamples();

PrintHeader("HMAC SHA256 HASH");
RunHmacSha256HashExamples();

PrintHeader("SEMVER");
RunSemVerExamples();

PrintHeader("URN");
RunUrnExamples();

PrintHeader("PERCENTAGE");
RunPercentageExamples();

PrintHeader("SECRET<T>");
RunSecretExamples();

PrintHeader("SNOWFLAKE ID (GELIŞMIŞ)");
RunSnowflakeAdvancedExamples();

PrintHeader("GERÇEK HAYAT SENARYOLARI");
RunRealWorldScenarios();

Console.WriteLine("\n✅ Tüm örnekler başarıyla çalıştı.");

// =============================================================================
// NON EMPTY STRING
// =============================================================================
void RunNonEmptyStringExamples() {

    // Temel oluşturma
    NonEmptyString username = NonEmptyString.Create("alice");
    Console.WriteLine($"Username: {username}");           // alice
    Console.WriteLine($"Length: {username.Length}");      // 5

    // TryCreate — exception fırlatmadan kontrol
    if(NonEmptyString.TryCreate("  ", out NonEmptyString bad)) {
        Console.WriteLine("Bu satır çalışmaz — whitespace geçersiz");
    }
    else {
        Console.WriteLine("Whitespace reddedildi ✓");
    }

    if(NonEmptyString.TryCreate("bob", out NonEmptyString bob)) {
        Console.WriteLine($"TryCreate başarılı: {bob}");
    }

    // Implicit cast — string gereken yere doğrudan geçilebilir
    string raw = username;  // implicit operator
    Console.WriteLine($"Implicit string cast: {raw}");

    // Explicit cast — string'den NonEmptyString'e
    NonEmptyString fromCast = (NonEmptyString)"charlie";
    Console.WriteLine($"Explicit cast: {fromCast}");

    // Parse / TryParse — span API
    NonEmptyString parsed = NonEmptyString.Parse("dave".AsSpan());
    Console.WriteLine($"Span parse: {parsed}");

    bool ok = NonEmptyString.TryParse("", out NonEmptyString emptyResult);
    Console.WriteLine($"Boş string parse: {ok} (beklenen: False)");

    // Karşılaştırma
    NonEmptyString a = NonEmptyString.Create("apple");
    NonEmptyString b = NonEmptyString.Create("banana");
    Console.WriteLine($"apple < banana: {a < b}");       // True (lexicographic)
    Console.WriteLine($"apple == apple: {a == NonEmptyString.Create("apple")}"); // True

    // JSON
    string json = JsonSerializer.Serialize(username, jsonOpts);
    Console.WriteLine($"JSON: {json}");
    NonEmptyString fromJson = JsonSerializer.Deserialize<NonEmptyString>(json, jsonOpts);
    Console.WriteLine($"JSON'dan: {fromJson}");

    // Dictionary key olarak kullanım
    var userRoles = new Dictionary<NonEmptyString, string[]>();
    userRoles[NonEmptyString.Create("alice")] = ["admin", "user"];
    Console.WriteLine($"Role count: {userRoles[NonEmptyString.Create("alice")].Length}");

    // Hata senaryoları
    try { NonEmptyString.Create(null!); }
    catch(ArgumentNullException e) { Console.WriteLine($"Null → {e.GetType().Name} ✓"); }

    try { NonEmptyString.Create("   "); }
    catch(ArgumentException e) { Console.WriteLine($"Whitespace → {e.GetType().Name} ✓"); }
}

// =============================================================================
// POSITIVE INT & NON NEGATIVE INT
// =============================================================================
void RunConstrainedIntExamples() {

    // PositiveInt
    PositiveInt pageSize = PositiveInt.Create(25);
    PositiveInt pageIndex = PositiveInt.Create(1);
    Console.WriteLine($"Sayfa boyutu: {pageSize}, Sayfa no: {pageIndex}");

    // Aritmetik — sonuç her zaman pozitif
    PositiveInt totalPages = pageSize + PositiveInt.Create(5); // 30
    PositiveInt factor = pageSize * PositiveInt.Create(2); // 50
    Console.WriteLine($"Toplam: {totalPages}, Çarpım: {factor}");

    // PositiveInt.One sabiti
    Console.WriteLine($"Minimum değer: {PositiveInt.One}"); // 1

    // Implicit cast to int
    int rawPageSize = pageSize;
    Console.WriteLine($"int'e implicit cast: {rawPageSize}");

    // Explicit cast from int
    PositiveInt fromInt = (PositiveInt)42;
    Console.WriteLine($"int'ten explicit cast: {fromInt}");

    // TryCreate
    bool ok = PositiveInt.TryCreate(0, out _);
    Console.WriteLine($"0 → PositiveInt: {ok} (beklenen: False)");
    ok = PositiveInt.TryCreate(-5, out _);
    Console.WriteLine($"-5 → PositiveInt: {ok} (beklenen: False)");
    ok = PositiveInt.TryCreate(1, out PositiveInt one);
    Console.WriteLine($"1 → PositiveInt: {ok}, değer: {one}");

    // Parse
    PositiveInt parsed = PositiveInt.Parse("100");
    Console.WriteLine($"Parse('100'): {parsed}");
    bool parseFail = PositiveInt.TryParse("0", out _);
    Console.WriteLine($"Parse('0'): {parseFail} (beklenen: False)");

    // JSON
    string json = JsonSerializer.Serialize(pageSize, jsonOpts);
    Console.WriteLine($"JSON: {json}");
    PositiveInt fromJson = JsonSerializer.Deserialize<PositiveInt>(json, jsonOpts);
    Console.WriteLine($"JSON'dan: {fromJson}");

    // NonNegativeInt
    NonNegativeInt offset = NonNegativeInt.Create(0);
    NonNegativeInt count = NonNegativeInt.Create(10);
    Console.WriteLine($"Offset: {offset}, Count: {count}");

    // Clamped çıkarma — asla negatif olmaz
    NonNegativeInt result = count - NonNegativeInt.Create(15); // 10 - 15 → 0 (clamped)
    Console.WriteLine($"10 - 15 (clamped): {result}");        // 0

    // PositiveInt → NonNegativeInt implicit (güvenli genişleme)
    NonNegativeInt widened = pageSize; // implicit
    Console.WriteLine($"PositiveInt → NonNegativeInt implicit: {widened}");

    // NonNegativeInt.Zero sabiti
    Console.WriteLine($"Zero: {NonNegativeInt.Zero}");

    // Karşılaştırma operatörleri
    NonNegativeInt x = NonNegativeInt.Create(5);
    NonNegativeInt y = NonNegativeInt.Create(10);
    Console.WriteLine($"5 < 10: {x < y}");
    Console.WriteLine($"5 >= 5: {x >= x}");

    // Hata senaryoları
    try { PositiveInt.Create(0); }
    catch(ArgumentOutOfRangeException e) { Console.WriteLine($"0 → {e.GetType().Name} ✓"); }

    try { NonNegativeInt.Create(-1); }
    catch(ArgumentOutOfRangeException e) { Console.WriteLine($"-1 → {e.GetType().Name} ✓"); }
}

// =============================================================================
// BOUNDED STRING
// =============================================================================
void RunBoundedStringExamples() {

    // Temel kullanım — Username: 3-50 karakter
    BoundedString<C3, C50> username = BoundedString<C3, C50>.Create("alice");
    Console.WriteLine($"Username: {username}");
    Console.WriteLine($"Length: {username.Length}");
    Console.WriteLine($"Min: {BoundedString<C3, C50>.AllowedMinLength}, Max: {BoundedString<C3, C50>.AllowedMaxLength}");

    // OTP kodu — tam 6 karakter
    BoundedString<C6, C6> otpCode = BoundedString<C6, C6>.Create("123456");
    Console.WriteLine($"OTP: {otpCode}");

    // Açıklama alanı — 0-512 karakter (C1 yerine TryCreate ile boş string'i yakala)
    BoundedString<C1, C512> desc = BoundedString<C1, C512>.Create("Bu bir açıklama metnidir.");
    Console.WriteLine($"Açıklama: {desc}");

    // TryCreate — exception'sız kontrol
    bool ok = BoundedString<C3, C50>.TryCreate("ab", out _); // 2 < 3
    Console.WriteLine($"2 char → [3,50]: {ok} (beklenen: False)");

    ok = BoundedString<C3, C50>.TryCreate(new string('x', 51), out _); // 51 > 50
    Console.WriteLine($"51 char → [3,50]: {ok} (beklenen: False)");

    ok = BoundedString<C3, C50>.TryCreate("valid", out BoundedString<C3, C50> valid);
    Console.WriteLine($"'valid' → [3,50]: {ok}, değer: {valid}");

    // Parse / TryParse span API
    BoundedString<C3, C50> parsed = BoundedString<C3, C50>.Parse("span-test".AsSpan());
    Console.WriteLine($"Span parse: {parsed}");

    // Explicit cast
    BoundedString<C3, C50> fromCast = (BoundedString<C3, C50>)"hello";
    Console.WriteLine($"Explicit cast: {fromCast}");

    // Implicit cast to string
    string raw = username;
    Console.WriteLine($"Implicit string: {raw}");

    // Karşılaştırma
    BoundedString<C3, C50> a = BoundedString<C3, C50>.Create("apple");
    BoundedString<C3, C50> b = BoundedString<C3, C50>.Create("banana");
    Console.WriteLine($"apple < banana: {a < b}");
    Console.WriteLine($"apple == apple: {a == BoundedString<C3, C50>.Create("apple")}");

    // JSON
    string json = JsonSerializer.Serialize(username, jsonOpts);
    Console.WriteLine($"JSON: {json}");
    BoundedString<C3, C50> fromJson = JsonSerializer.Deserialize<BoundedString<C3, C50>>(json, jsonOpts);
    Console.WriteLine($"JSON'dan: {fromJson}");

    // Özel tip tanımı — kendi IIntConstant sabitlerin
    // public readonly struct C7 : IIntConstant { public static int Value => 7; }
    // BoundedString<C7, C7> weekCode = BoundedString<C7, C7>.Create("MON2024");

    // Hata senaryoları
    try { BoundedString<C3, C50>.Create("ab"); }
    catch(ArgumentException e) { Console.WriteLine($"Kısa string → {e.GetType().Name} ✓"); }

    try { BoundedString<C3, C50>.Create(new string('x', 51)); }
    catch(ArgumentException e) { Console.WriteLine($"Uzun string → {e.GetType().Name} ✓"); }
}

// =============================================================================
// INTERVAL<T>
// =============================================================================
void RunIntervalExamples() {

    // int interval — kapalı [1, 10]
    Interval<int> closed = Interval<int>.Closed(1, 10);
    Console.WriteLine($"Kapalı aralık: {closed}");          // [1, 10]
    Console.WriteLine($"5 içinde mi: {closed.Contains(5)}");  // True
    Console.WriteLine($"1 içinde mi: {closed.Contains(1)}");  // True (kapalı)
    Console.WriteLine($"10 içinde mi: {closed.Contains(10)}"); // True (kapalı)
    Console.WriteLine($"11 içinde mi: {closed.Contains(11)}"); // False

    // Açık (0, 1) — uç değerler hariç
    Interval<double> open = Interval<double>.Open(0.0, 1.0);
    Console.WriteLine($"\nAçık aralık: {open}");             // (0, 1)
    Console.WriteLine($"0.5 içinde mi: {open.Contains(0.5)}"); // True
    Console.WriteLine($"0.0 içinde mi: {open.Contains(0.0)}"); // False (açık)
    Console.WriteLine($"1.0 içinde mi: {open.Contains(1.0)}"); // False (açık)

    // Yarı açık [start, end) — tarih aralıkları için en yaygın
    DateTimeOffset q1Start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    DateTimeOffset q1End = new(2025, 4, 1, 0, 0, 0, TimeSpan.Zero);
    Interval<DateTimeOffset> q1 = Interval<DateTimeOffset>.ClosedOpen(q1Start, q1End);
    Console.WriteLine($"\nQ1 2025: {q1}");

    DateTimeOffset march15 = new(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
    DateTimeOffset april1 = new(2025, 4, 1, 0, 0, 0, TimeSpan.Zero);
    Console.WriteLine($"15 Mart içinde mi: {q1.Contains(march15)}"); // True
    Console.WriteLine($"1 Nisan içinde mi: {q1.Contains(april1)}");  // False (açık uç)

    // UnixTimestamp ile kullanım
    UnixTimestamp tsStart = UnixTimestamp.FromSeconds(1_700_000_000);
    UnixTimestamp tsEnd = UnixTimestamp.FromSeconds(1_800_000_000);
    Interval<UnixTimestamp> tsInterval = Interval<UnixTimestamp>.ClosedOpen(tsStart, tsEnd);
    Console.WriteLine($"\nTimestamp aralığı: {tsInterval}");

    UnixTimestamp tsMiddle = UnixTimestamp.FromSeconds(1_750_000_000);
    Console.WriteLine($"Ortada mı: {tsInterval.Contains(tsMiddle)}"); // True

    // Overlaps — iki aralık örtüşüyor mu
    Interval<int> a = Interval<int>.Closed(1, 5);
    Interval<int> b = Interval<int>.Closed(3, 8);
    Interval<int> c = Interval<int>.Closed(6, 10);
    Console.WriteLine($"\n[1,5] ∩ [3,8] örtüşüyor mu: {a.Overlaps(b)}"); // True
    Console.WriteLine($"[1,5] ∩ [6,10] örtüşüyor mu: {a.Overlaps(c)}"); // False

    // TryIntersect — kesişim hesaplama
    if(a.TryIntersect(b, out Interval<int> intersection)) {
        Console.WriteLine($"[1,5] ∩ [3,8] = {intersection}"); // [3, 5]
    }

    // IsSubsetOf
    Interval<int> small = Interval<int>.Closed(2, 4);
    Interval<int> large = Interval<int>.Closed(1, 10);
    Console.WriteLine($"[2,4] ⊆ [1,10]: {small.IsSubsetOf(large)}"); // True
    Console.WriteLine($"[1,10] ⊆ [2,4]: {large.IsSubsetOf(small)}"); // False

    // IsEmpty — (5, 5) boş aralık
    Interval<int> empty = Interval<int>.Open(5, 5);
    Console.WriteLine($"\n(5,5) boş mu: {empty.IsEmpty}"); // True

    Interval<int> notEmpty = Interval<int>.Closed(5, 5);
    Console.WriteLine($"[5,5] boş mu: {notEmpty.IsEmpty}"); // False

    // Deconstruct
    var (start, end) = closed;
    Console.WriteLine($"\nDeconstruct: start={start}, end={end}");

    // JSON
    string json = JsonSerializer.Serialize(closed, jsonOpts);
    Console.WriteLine($"\nJSON:\n{json}");
    Interval<int> fromJson = JsonSerializer.Deserialize<Interval<int>>(json, jsonOpts);
    Console.WriteLine($"JSON'dan: {fromJson}");

    // TryCreate — geçersiz aralık (start > end)
    bool ok = Interval<int>.TryCreate(10, 5,
        IntervalBoundary.Closed, IntervalBoundary.Closed, out _);
    Console.WriteLine($"\nStart > End TryCreate: {ok} (beklenen: False)");

    // Hata senaryosu
    try { Interval<int>.Closed(10, 5); }
    catch(ArgumentException e) { Console.WriteLine($"Start > End → {e.GetType().Name} ✓"); }
}

// =============================================================================
// MIME TYPE
// =============================================================================
void RunMimeTypeExamples() {

    // Well-known static property'ler
    Console.WriteLine($"ApplicationJson: {MimeType.ApplicationJson}");
    Console.WriteLine($"TextHtml: {MimeType.TextHtml}");
    Console.WriteLine($"ImagePng: {MimeType.ImagePng}");
    Console.WriteLine($"MultipartFormData: {MimeType.MultipartFormData}");
    Console.WriteLine($"ApplicationProblemJson: {MimeType.ApplicationProblemJson}");

    // Parse — case insensitive, lowercase'e normalize eder
    MimeType json1 = MimeType.Parse("application/json");
    MimeType json2 = MimeType.Parse("Application/JSON");   // normalize → application/json
    Console.WriteLine($"\nCase normalize: {json1 == json2}"); // True

    // Parametreler otomatik trim edilir
    MimeType withCharset = MimeType.Parse("text/html; charset=utf-8");
    Console.WriteLine($"Charset trim: {withCharset}"); // text/html

    // Type ve Subtype erişimi
    MimeType mime = MimeType.Parse("application/vnd.api+json");
    Console.WriteLine($"\nType: {mime.Type}");          // application
    Console.WriteLine($"Subtype: {mime.Subtype}");      // vnd.api+json
    Console.WriteLine($"HasSuffix: {mime.HasSuffix}"); // True

    // Yardımcı property'ler
    Console.WriteLine($"\nIsText(text/plain): {MimeType.TextPlain.IsText}");         // True
    Console.WriteLine($"IsImage(image/png): {MimeType.ImagePng.IsImage}");           // True
    Console.WriteLine($"IsApplication(app/json): {MimeType.ApplicationJson.IsApplication}"); // True
    Console.WriteLine($"IsAudio(audio/mpeg): {MimeType.AudioMpeg.IsAudio}");         // True

    // TryParse
    bool ok = MimeType.TryParse("application/json", out MimeType parsed);
    Console.WriteLine($"\nTryParse geçerli: {ok}, değer: {parsed}");

    ok = MimeType.TryParse("gecersiz", out _);
    Console.WriteLine($"TryParse slash yok: {ok} (beklenen: False)");

    ok = MimeType.TryParse("/subtype", out _);
    Console.WriteLine($"TryParse boş type: {ok} (beklened: False)");

    ok = MimeType.TryParse("type/", out _);
    Console.WriteLine($"TryParse boş subtype: {ok} (beklened: False)");

    // Span API
    MimeType spanParsed = MimeType.Parse("image/webp".AsSpan());
    Console.WriteLine($"\nSpan parse: {spanParsed}");

    // Implicit / explicit cast
    string raw = MimeType.ApplicationJson;          // implicit
    MimeType fromCast = (MimeType)"text/csv";       // explicit
    Console.WriteLine($"Implicit: {raw}");
    Console.WriteLine($"Explicit: {fromCast}");

    // Eşitlik
    Console.WriteLine($"\nEşitlik: {MimeType.ApplicationJson == MimeType.Parse("application/json")}"); // True

    // JSON
    string json = JsonSerializer.Serialize(MimeType.ApplicationJson, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
    MimeType fromJson = JsonSerializer.Deserialize<MimeType>(json, jsonOpts);
    Console.WriteLine($"JSON'dan: {fromJson}");

    // Switch expression ile kullanım
    MimeType contentType = MimeType.ImagePng;
    string category = contentType switch {
        _ when contentType.IsImage => "Görsel içerik",
        _ when contentType.IsText => "Metin içerik",
        _ when contentType.IsAudio => "Ses içerik",
        _ => "Diğer"
    };
    Console.WriteLine($"\nKategori: {category}"); // Görsel içerik

    // Hata senaryosu
    try { MimeType.Parse("gecersiz-format"); }
    catch(FormatException e) { Console.WriteLine($"Geçersiz format → {e.GetType().Name} ✓"); }
}

// =============================================================================
// GUID V7
// =============================================================================
void RunGuidV7Examples() {

    // Temel üretim
    GuidV7 id1 = GuidV7.Create();
    GuidV7 id2 = GuidV7.Create();
    Console.WriteLine($"ID 1: {id1}");
    Console.WriteLine($"ID 2: {id2}");
    Console.WriteLine($"Farklı mı: {id1 != id2}");     // True
    Console.WriteLine($"Sıralı mı (id1 <= id2): {id1 <= id2}"); // True (k-sorted)

    // TimeProvider ile deterministik üretim
    DateTimeOffset fixedTime = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    GuidV7 seededId = GuidV7.Create(new FixedTimeProvider(fixedTime));
    Console.WriteLine($"\nFixed time ID: {seededId}");

    // UnixTimestamp ile üretim
    UnixTimestamp ts = UnixTimestamp.FromMilliseconds(1_748_000_000_000L);
    GuidV7 tsId = GuidV7.Create(ts);
    Console.WriteLine($"UnixTimestamp ID: {tsId}");

    // Timestamp extraction
    DateTimeOffset extracted = id1.GetTimestamp();
    Console.WriteLine($"\nEmbedded timestamp: {extracted:O}");
    Console.WriteLine($"UnixTimestamp: {id1.UnixTimestamp.TotalMilliseconds} ms");

    // Format seçenekleri
    Console.WriteLine($"\nFormat D: {id1.ToString("D")}");  // 00000000-0000-7000-...
    Console.WriteLine($"Format N: {id1.ToString("N")}");    // 000000007000...
    Console.WriteLine($"Format B: {id1.ToString("B")}");    // {00000000-...}
    Console.WriteLine($"Format P: {id1.ToString("P")}");    // (00000000-...)

    // Dönüşümler
    HexString hex = id1.ToHexString();
    Base64UrlString b64url = id1.ToBase64Url();
    Console.WriteLine($"\nHex (32 char): {hex}");
    Console.WriteLine($"Base64Url (22 char): {b64url}");

    // Implicit / explicit cast
    Guid underlyingGuid = id1;           // implicit
    GuidV7 fromGuid = (GuidV7)Guid.CreateVersion7(); // explicit
    Console.WriteLine($"\nGuid'e implicit: {underlyingGuid}");
    Console.WriteLine($"Guid'den explicit: {fromGuid}");

    // Parse / TryParse
    GuidV7 parsed = GuidV7.Parse(id1.ToString());
    Console.WriteLine($"\nParse roundtrip: {parsed == id1}"); // True

    bool ok = GuidV7.TryParse(Guid.NewGuid().ToString(), out _); // v4
    Console.WriteLine($"v4 → TryParse: {ok} (beklened: False)");

    // ISpanFormattable
    Span<char> charBuf = stackalloc char[36];
    ((ISpanFormattable)id1).TryFormat(charBuf, out int written, "D", null);
    Console.WriteLine($"\nSpan format: {new string(charBuf[..written])}");

    // IUtf8SpanFormattable
    Span<byte> utf8Buf = stackalloc byte[36];
    ((IUtf8SpanFormattable)id1).TryFormat(utf8Buf, out int bytesWritten, "D", null);
    Console.WriteLine($"UTF8 format: {System.Text.Encoding.UTF8.GetString(utf8Buf[..bytesWritten])}");

    // Sıralama
    var ids = Enumerable.Range(0, 5).Select(_ => GuidV7.Create()).ToList();
    var sorted = ids.OrderBy(x => x).ToList();
    Console.WriteLine($"\n5 ID sıralı mı: {ids.SequenceEqual(sorted)}"); // True

    // JSON
    string json = JsonSerializer.Serialize(id1, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
    GuidV7 fromJson = JsonSerializer.Deserialize<GuidV7>(json, jsonOpts);
    Console.WriteLine($"JSON roundtrip: {fromJson == id1}");

    // Hata senaryoları
    try { GuidV7.Parse(Guid.NewGuid().ToString()); }
    catch(FormatException e) { Console.WriteLine($"\nv4 parse → {e.GetType().Name} ✓"); }

    try { _ = (GuidV7)Guid.Empty; }
    catch(InvalidCastException e) { Console.WriteLine($"Empty guid cast → {e.GetType().Name} ✓"); }
}


// =============================================================================
// UNIX TIMESTAMP
// =============================================================================
void RunUnixTimestampExamples() {

    // Şu anki zaman
    UnixTimestamp now = UnixTimestamp.Now;
    Console.WriteLine($"Şimdi: {now}");                        // ISO 8601
    Console.WriteLine($"Milliseconds: {now.TotalMilliseconds}");
    Console.WriteLine($"Seconds: {now.TotalSeconds}");

    // Factory metodları
    UnixTimestamp fromSeconds = UnixTimestamp.FromSeconds(1_700_000_000);
    UnixTimestamp fromMs = UnixTimestamp.FromMilliseconds(1_700_000_000_000L);
    UnixTimestamp fromDto = UnixTimestamp.From(DateTimeOffset.UtcNow);
    Console.WriteLine($"\nFromSeconds: {fromSeconds}");
    Console.WriteLine($"FromMs: {fromMs}");
    Console.WriteLine($"FromDto: {fromDto}");

    // Dönüşümler
    DateTimeOffset dto = fromSeconds.ToDateTimeOffset();
    DateTime utc = fromSeconds.ToDateTimeUtc();
    DateTime local = fromSeconds.ToDateTimeLocal();
    Console.WriteLine($"\nDateTimeOffset: {dto:O}");
    Console.WriteLine($"UTC: {utc:O}");
    Console.WriteLine($"Local: {local}");

    // Aritmetik
    UnixTimestamp future = now + TimeSpan.FromDays(30);
    UnixTimestamp past = now - TimeSpan.FromHours(24);
    TimeSpan diff = future - now;
    Console.WriteLine($"\n30 gün sonra: {future}");
    Console.WriteLine($"24 saat önce: {past}");
    Console.WriteLine($"Fark: {diff.TotalDays:F1} gün");

    // Karşılaştırma
    Console.WriteLine($"\nFuture > Now: {future > now}"); // True
    Console.WriteLine($"Past < Now: {past < now}");       // True

    // Sabitler
    Console.WriteLine($"\nEpoch: {UnixTimestamp.Epoch}");
    Console.WriteLine($"MinValue: {UnixTimestamp.MinValue}");
    Console.WriteLine($"MaxValue: {UnixTimestamp.MaxValue}");

    // Format
    Console.WriteLine($"\nDefault: {now}");
    Console.WriteLine($"Raw ms (R): {now.ToString("R")}");
    Console.WriteLine($"Custom: {now.ToString("yyyy-MM-dd")}");

    // Parse
    UnixTimestamp parsed = UnixTimestamp.Parse("1700000000000");
    Console.WriteLine($"\nParse: {parsed}");

    // Implicit / explicit cast
    long ms = now;                                              // implicit
    UnixTimestamp fromLong = (UnixTimestamp)1_700_000_000_000L; // explicit
    Console.WriteLine($"long: {ms}");
    Console.WriteLine($"FromLong: {fromLong}");

    // JSON
    string json = JsonSerializer.Serialize(now, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
}

// =============================================================================
// SNOWFLAKE ID
// =============================================================================
void RunSnowflakeIdExamples() {

    // Konfigürasyon — uygulama başlangıcında bir kez yapılır
    SnowflakeId.Configure(nodeId: 1);

    SnowflakeId id1 = SnowflakeId.NewId();
    SnowflakeId id2 = SnowflakeId.NewId();
    Console.WriteLine($"ID 1: {id1}");
    Console.WriteLine($"ID 2: {id2}");
    Console.WriteLine($"Sıralı mı: {id1 < id2}"); // True

    // Format dönüşümleri
    Console.WriteLine($"\nHex: {id1.ToHexString()}");
    Console.WriteLine($"Base62: {id1.ToBase62String()}");

    // JSON
    string json = JsonSerializer.Serialize(id1, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
}

// =============================================================================
// HEX STRING
// =============================================================================
void RunHexStringExamples() {

    // FromBytes
    byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];
    HexString hex = HexString.FromBytes(data);
    Console.WriteLine($"FromBytes: {hex}"); // DEADBEEF

    // FromUtf8
    HexString fromText = HexString.FromUtf8("hello");
    Console.WriteLine($"FromUtf8: {fromText}"); // 68656c6c6f

    // Parse
    HexString parsed = HexString.Parse("DEADBEEF");
    Console.WriteLine($"\nParse: {parsed}");

    bool ok = HexString.TryParse("GGGG", out _); // geçersiz
    Console.WriteLine($"Geçersiz hex: {ok} (beklened: False)");

    ok = HexString.TryParse("ABC", out _); // tek sayı (% 2 != 0)
    Console.WriteLine($"Tek char: {ok} (beklened: False)");

    // Decode
    byte[] decoded = hex.ToBytes();
    Console.WriteLine($"\nDecode: {BitConverter.ToString(decoded)}"); // DE-AD-BE-EF

    Span<byte> stackDecode = stackalloc byte[4];
    hex.TryDecode(stackDecode, out int written);
    Console.WriteLine($"Stack decode: {written} bytes");

    // Case dönüşümü
    HexString upper = hex.ToUpper();
    HexString lower = hex.ToLower();
    Console.WriteLine($"\nUpper: {upper}");
    Console.WriteLine($"Lower: {lower}");

    // ISpanFormattable format "x" = lowercase
    Span<char> buf = stackalloc char[8];
    ((ISpanFormattable)hex).TryFormat(buf, out int fw, "x", null);
    Console.WriteLine($"Format 'x': {new string(buf[..fw])}");

    // WriteTo
    var writer = new System.Buffers.ArrayBufferWriter<byte>();
    hex.WriteTo(writer);
    Console.WriteLine($"WriteTo: {System.Text.Encoding.UTF8.GetString(writer.WrittenSpan)}");

    // Sha256Hash dönüşümü
    Sha256Hash hash = Sha256Hash.Compute("test");
    HexString hashHex = hash.ToHexString();
    Sha256Hash backToHash = hashHex.ToSha256Hash();
    Console.WriteLine($"\nHash roundtrip: {hash == backToHash}");
}

// =============================================================================
// BASE64 STRING
// =============================================================================
void RunBase64StringExamples() {

    // FromBytes
    byte[] data = [1, 2, 3, 4, 5];
    Base64String b64 = Base64String.FromBytes(data);
    Console.WriteLine($"FromBytes: {b64}");

    // FromUtf8
    Base64String fromText = Base64String.FromUtf8("Hello World");
    Console.WriteLine($"FromUtf8: {fromText}"); // SGVsbG8gV29ybGQ=

    // From — farklı encoding
    Base64String fromEncoding = Base64String.From("Merhaba", System.Text.Encoding.UTF8);
    Console.WriteLine($"FromEncoding: {fromEncoding}");

    // Parse
    Base64String parsed = Base64String.Parse("SGVsbG8gV29ybGQ=");
    Console.WriteLine($"\nParse: {parsed}");

    bool ok = Base64String.TryParse("!!!invalid!!!", out _);
    Console.WriteLine($"Geçersiz: {ok} (beklened: False)");

    // Decode
    byte[] decoded = fromText.ToBytes();
    Console.WriteLine($"\nDecode: {System.Text.Encoding.UTF8.GetString(decoded)}"); // Hello World

    Span<byte> stackDecode = stackalloc byte[32];
    fromText.TryDecode(stackDecode, out int written);
    Console.WriteLine($"Stack decode: {System.Text.Encoding.UTF8.GetString(stackDecode[..written])}");

    // GetDecodedLength
    Console.WriteLine($"Decoded length: {fromText.GetDecodedLength()}");

    // WriteTo
    var writer = new System.Buffers.ArrayBufferWriter<byte>();
    b64.WriteTo(writer);
    Console.WriteLine($"WriteTo bytes: {writer.WrittenCount}");

    // ISpanFormattable
    Span<char> buf = stackalloc char[64];
    ((ISpanFormattable)b64).TryFormat(buf, out int fw, default, null);
    Console.WriteLine($"Span format: {new string(buf[..fw])}");

    // JSON
    string json = JsonSerializer.Serialize(fromText, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
    Base64String fromJson = JsonSerializer.Deserialize<Base64String>(json, jsonOpts);
    Console.WriteLine($"Roundtrip: {fromJson == fromText}");
}

// =============================================================================
// BASE64 URL STRING
// =============================================================================
void RunBase64UrlStringExamples() {

    // FromBytes — +/= yerine -_ ve padding yok
    byte[] data = Guid.CreateVersion7().ToByteArray();
    Base64UrlString b64url = Base64UrlString.FromBytes(data);
    Console.WriteLine($"FromBytes: {b64url} (length: {b64url.Value.Length})"); // 22 char

    // Parse — padding kabul etmez
    Base64UrlString parsed = Base64UrlString.Parse("SGVsbG8gV29ybGQ");
    Console.WriteLine($"Parse: {parsed}");

    bool ok = Base64UrlString.TryParse("with+plus", out _); // + geçersiz
    Console.WriteLine($"'+' içeren: {ok} (beklened: False)");

    ok = Base64UrlString.TryParse("with/slash", out _); // / geçersiz
    Console.WriteLine($"'/' içeren: {ok} (beklened: False)");

    // Decode
    byte[] decoded = b64url.ToBytes();
    Console.WriteLine($"\nDecode: {decoded.Length} bytes");

    Span<byte> stackDecode = stackalloc byte[16];
    b64url.TryDecode(stackDecode, out int written);
    Console.WriteLine($"Stack decode: {written} bytes");

    // GuidV7 ile birlikte kullanım
    GuidV7 guidId = GuidV7.Create();
    Base64UrlString compact = guidId.ToBase64Url();
    Console.WriteLine($"\nGuidV7 compact: {compact}"); // 22 char URL-safe

    // ISpanFormattable
    Span<char> buf = stackalloc char[32];
    ((ISpanFormattable)compact).TryFormat(buf, out int fw, default, null);
    Console.WriteLine($"Span format: {new string(buf[..fw])}");
}

// =============================================================================
// BASE32 STRING
// =============================================================================
void RunBase32StringExamples() {

    // FromBytes
    byte[] data = [104, 101, 108, 108, 111]; // "hello"
    Base32String b32 = Base32String.FromBytes(data);
    Console.WriteLine($"FromBytes: {b32}"); // NBSWY3DP

    // FromUtf8
    Base32String fromText = Base32String.FromUtf8("hello");
    Console.WriteLine($"FromUtf8: {fromText}"); // NBSWY3DP

    // Parse — case insensitive, uppercase normalize
    Base32String fromLower = Base32String.Parse("nbswy3dp");
    Base32String fromUpper = Base32String.Parse("NBSWY3DP");
    Console.WriteLine($"\nCase normalize eşit mi: {fromLower == fromUpper}"); // True

    bool ok = Base32String.TryParse("!@#$", out _); // geçersiz
    Console.WriteLine($"Geçersiz: {ok} (beklened: False)");

    // Decode
    byte[] decoded = fromText.ToBytes();
    Console.WriteLine($"\nDecode: {System.Text.Encoding.UTF8.GetString(decoded)}"); // hello

    Span<byte> stackDecode = stackalloc byte[8];
    fromText.TryDecode(stackDecode, out int written);
    Console.WriteLine($"Stack decoded: {System.Text.Encoding.UTF8.GetString(stackDecode[..written])}");

    // GetDecodedLength
    Console.WriteLine($"Decoded length: {fromText.GetDecodedLength()}"); // 5

    // TOTP için tipik kullanım — 20 byte secret → Base32
    byte[] totpSecret = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20);
    Base32String totpKey = Base32String.FromBytes(totpSecret);
    Console.WriteLine($"\nTOTP Secret: {totpKey}");

    // WriteTo
    var writer = new System.Buffers.ArrayBufferWriter<byte>();
    fromText.WriteTo(writer);
    Console.WriteLine($"WriteTo: {System.Text.Encoding.UTF8.GetString(writer.WrittenSpan)}");

    // JSON
    string json = JsonSerializer.Serialize(fromText, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
}

// =============================================================================
// BASE62 STRING
// =============================================================================
void RunBase62StringExamples() {

    // FromInt64 — SnowflakeId gibi sayıları kompakt göstermek için
    Base62String fromLong = Base62String.FromInt64(123456789012345678L);
    Console.WriteLine($"FromInt64: {fromLong}");

    // FromBytes
    byte[] data = Guid.NewGuid().ToByteArray();
    Base62String fromBytes = Base62String.FromBytes(data);
    Console.WriteLine($"FromBytes: {fromBytes}");

    // Parse
    Base62String parsed = Base62String.Parse("Hello123");
    Console.WriteLine($"\nParse: {parsed}");

    bool ok = Base62String.TryParse("hello-world", out _); // '-' geçersiz
    Console.WriteLine($"'-' içeren: {ok} (beklened: False)");

    // ToInt64 — geri dönüşüm
    Base62String encoded = Base62String.FromInt64(999999);
    long decoded = encoded.ToInt64();
    Console.WriteLine($"\n999999 → {encoded} → {decoded}");
    Console.WriteLine($"Roundtrip: {decoded == 999999}");

    // ToBytes — roundtrip
    byte[] original = [1, 2, 3, 4, 5, 6, 7, 8];
    Base62String b62 = Base62String.FromBytes(original);
    byte[] back = b62.ToBytes();
    Console.WriteLine($"\nBytes roundtrip: {original.SequenceEqual(back)}");

    // ISpanFormattable
    Span<char> buf = stackalloc char[32];
    ((ISpanFormattable)encoded).TryFormat(buf, out int fw, default, null);
    Console.WriteLine($"Span format: {new string(buf[..fw])}");

    // JSON
    string json = JsonSerializer.Serialize(fromLong, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
}

// =============================================================================
// SHA256 HASH
// =============================================================================
void RunSha256HashExamples() {

    // Compute — string
    Sha256Hash hash1 = Sha256Hash.Compute("Hello World");
    Console.WriteLine($"Hash: {hash1}");

    // Compute — bytes (zero alloc)
    byte[] data = [1, 2, 3, 4, 5];
    Sha256Hash hash2 = Sha256Hash.Compute(data.AsSpan());
    Console.WriteLine($"Bytes hash: {hash2}");

    // Compute — farklı encoding
    Sha256Hash hash3 = Sha256Hash.Compute("Merhaba", System.Text.Encoding.UTF8);
    Console.WriteLine($"UTF8 hash: {hash3}");

    // Async stream hashing
    // using var stream = File.OpenRead("file.bin");
    // Sha256Hash fileHash = await Sha256Hash.ComputeAsync(stream);

    // Timing-safe eşitlik
    Sha256Hash same = Sha256Hash.Compute("Hello World");
    Console.WriteLine($"\nEşitlik (timing-safe): {hash1 == same}"); // True
    Console.WriteLine($"Farklı: {hash1 == hash2}");                 // False

    // Dönüşümler
    HexString hex = hash1.ToHexString();
    Base64String b64 = hash1.ToBase64String();
    Console.WriteLine($"\nHex: {hex}");
    Console.WriteLine($"Base64: {b64}");

    // FromHex / FromBase64
    Sha256Hash fromHex = Sha256Hash.From(hex);
    Sha256Hash fromB64 = Sha256Hash.From(b64);
    Console.WriteLine($"FromHex roundtrip: {hash1 == fromHex}");
    Console.WriteLine($"FromBase64 roundtrip: {hash1 == fromB64}");

    // AsSpan — direct byte access
    hash1.Expose(span => {
        Console.WriteLine($"\nFirst byte: 0x{span[0]:X2}");
        Console.WriteLine($"Span length: {span.Length}"); // 32
    });

    // CopyTo
    Span<byte> destination = stackalloc byte[32];
    hash1.CopyTo(destination);
    Console.WriteLine($"CopyTo: {destination.Length} bytes");

    // ISpanFormattable
    Span<char> buf = stackalloc char[64];
    ((ISpanFormattable)hash1).TryFormat(buf, out int fw, default, null);
    Console.WriteLine($"\nSpan format (upper): {new string(buf[..fw])}");

    ((ISpanFormattable)hash1).TryFormat(buf, out fw, "x", null);
    Console.WriteLine($"Span format 'x' (lower): {new string(buf[..fw])}");

    // GetHashCode (non-cryptographic, collection use)
    Console.WriteLine($"GetHashCode: {hash1.GetHashCode()}");
}

// =============================================================================
// HMAC SHA256 HASH
// =============================================================================
void RunHmacSha256HashExamples() {

    byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
                   17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
    byte[] data = System.Text.Encoding.UTF8.GetBytes("message");

    // Compute — raw spans (zero alloc)
    HmacSha256Hash hmac1 = HmacSha256Hash.Compute(key.AsSpan(), data.AsSpan());
    Console.WriteLine($"HMAC: {hmac1}");

    // Dönüşümler
    HexString hex = hmac1.ToHexString();
    Base64String b64 = hmac1.ToBase64String();
    Base64UrlString b64url = hmac1.ToBase64UrlString();
    Console.WriteLine($"\nHex: {hex}");
    Console.WriteLine($"Base64: {b64}");
    Console.WriteLine($"Base64Url: {b64url}");

    // FromHex / FromBase64 roundtrip
    HmacSha256Hash fromHex = HmacSha256Hash.FromHex(hex);
    HmacSha256Hash fromB64 = HmacSha256Hash.FromBase64(b64);
    Console.WriteLine($"\nFromHex roundtrip: {hmac1 == fromHex}");
    Console.WriteLine($"FromBase64 roundtrip: {hmac1 == fromB64}");

    // AsSpan
    Console.WriteLine($"AsSpan length: {hmac1.AsSpan().Length}"); // 32

    // TryCopyTo
    Span<byte> dest = stackalloc byte[32];
    bool copied = hmac1.TryCopyTo(dest);
    Console.WriteLine($"TryCopyTo: {copied}");

    // Implicit → ReadOnlySpan<byte>
    ReadOnlySpan<byte> asSpan = hmac1; // implicit
    Console.WriteLine($"Implicit span length: {asSpan.Length}");

    // Timing-safe eşitlik
    HmacSha256Hash same = HmacSha256Hash.Compute(key.AsSpan(), data.AsSpan());
    Console.WriteLine($"\nTiming-safe eşitlik: {hmac1 == same}"); // True

    // ISpanFormattable
    Span<char> buf = stackalloc char[64];
    ((ISpanFormattable)hmac1).TryFormat(buf, out int fw, default, null);
    Console.WriteLine($"Span format: {new string(buf[..fw])}");

    // JSON
    string json = JsonSerializer.Serialize(hmac1, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
}

// =============================================================================
// SEMVER
// =============================================================================
void RunSemVerExamples() {

    // Oluşturma
    SemVer v1 = new(1, 0, 0);
    SemVer v2 = new(2, 3, 1);
    SemVer alpha = new(1, 0, 0, "alpha.1");
    SemVer beta = new(1, 0, 0, "beta");
    SemVer full = new(1, 2, 3, "rc.1", "build.20250601");

    Console.WriteLine($"Stable: {v1}");
    Console.WriteLine($"Version: {v2}");
    Console.WriteLine($"Alpha: {alpha}");
    Console.WriteLine($"Beta: {beta}");
    Console.WriteLine($"Full: {full}");

    // Karşılaştırma — SemVer kurallarına göre
    Console.WriteLine($"\n1.0.0-alpha < 1.0.0-beta: {alpha < beta}"); // True
    Console.WriteLine($"1.0.0-alpha < 1.0.0: {alpha < v1}");          // True (pre-release < stable)
    Console.WriteLine($"1.0.0 < 2.3.1: {v1 < v2}");                  // True

    // IsPreRelease
    Console.WriteLine($"\nv1.IsPreRelease: {v1.IsPreRelease}");       // False
    Console.WriteLine($"alpha.IsPreRelease: {alpha.IsPreRelease}");   // True

    // Format seçenekleri
    Console.WriteLine($"\nG (default): {full.ToString("G")}");        // 1.2.3-rc.1+build.20250601
    Console.WriteLine($"f (full no meta): {full.ToString("f")}");     // 1.2.3-rc.1
    Console.WriteLine($"s (stable): {full.ToString("s")}");           // 1.2.3
    Console.WriteLine($"m (major.minor): {full.ToString("m")}");      // 1.2
    Console.WriteLine($"M (major): {full.ToString("M")}");            // 1

    // Parse
    SemVer parsed = SemVer.Parse("1.2.3-alpha.1+build.001");
    Console.WriteLine($"\nParse: {parsed}");
    Console.WriteLine($"Major: {parsed.Major}, Minor: {parsed.Minor}, Patch: {parsed.Patch}");
    Console.WriteLine($"PreRelease: {parsed.PreRelease}");
    Console.WriteLine($"BuildMetadata: {parsed.BuildMetadata}");

    bool ok = SemVer.TryParse("not-a-version", out _);
    Console.WriteLine($"Geçersiz parse: {ok} (beklened: False)");

    ok = SemVer.TryParse("01.0.0", out _); // leading zero
    Console.WriteLine($"Leading zero: {ok} (beklened: False)");

    // System.Version'dan dönüşüm
    Version sysVersion = new(3, 1, 2, 4);
    SemVer fromVersion = (SemVer)sysVersion;
    Console.WriteLine($"\nSystem.Version → SemVer: {fromVersion}");

    // TryFormat
    Span<char> buf = stackalloc char[128];
    full.TryFormat(buf, out int written, "G".AsSpan());
    Console.WriteLine($"TryFormat: {new string(buf[..written])}");

    // Sıralama
    var versions = new[] { v2, alpha, v1, beta };
    var sorted = versions.OrderBy(v => v).ToArray();
    Console.WriteLine($"\nSıralı: {string.Join(", ", sorted.Select(v => v.ToString()))}");

    // MinVersion
    Console.WriteLine($"MinVersion: {SemVer.MinVersion}");

    // JSON
    string json = JsonSerializer.Serialize(v2);
    Console.WriteLine($"\nJSON: {json}");
}

// =============================================================================
// URN
// =============================================================================
void RunUrnExamples() {

    // String nss ile oluşturma
    Urn userUrn = Urn.Create("user", "alice123");
    Urn orderUrn = Urn.Create("order", "550e8400-e29b-41d4-a716-446655440000");
    Console.WriteLine($"User URN: {userUrn}");   // urn:user:alice123
    Console.WriteLine($"Order URN: {orderUrn}");

    // Guid ile oluşturma — zero-alloc
    Urn guidUrn = Urn.Create("session", Guid.CreateVersion7());
    Console.WriteLine($"Guid URN: {guidUrn}");

    // SnowflakeId ile oluşturma
    SnowflakeId.Configure(nodeId: 1);
    Urn snowflakeUrn = Urn.Create("resource", SnowflakeId.NewId());
    Console.WriteLine($"Snowflake URN: {snowflakeUrn}");

    // Hiyerarşik URN
    Urn hierarchical = Urn.Create("org", "acme", "department", "engineering");
    Console.WriteLine($"\nHiyerarşik: {hierarchical}"); // urn:org:acme:department:engineering

    Urn twoLevel = Urn.Create("file", "2025", "report.pdf");
    Console.WriteLine($"İki seviye: {twoLevel}"); // urn:file:2025:report.pdf

    // Namespace ve Identity erişimi (zero-alloc span)
    Console.WriteLine($"\nNamespace: {userUrn.Namespace.ToString()}"); // user
    Console.WriteLine($"Identity: {userUrn.Identity.ToString()}");     // alice123

    // Deconstruct
    var (nid, nss) = userUrn;
    Console.WriteLine($"Deconstruct: nid={nid.ToString()}, nss={nss.ToString()}");

    // Parse
    Urn parsed = Urn.Parse("urn:book:978-0-123-45678-9");
    Console.WriteLine($"\nParse: {parsed}");
    Console.WriteLine($"Namespace: {parsed.Namespace.ToString()}");
    Console.WriteLine($"Identity: {parsed.Identity.ToString()}");

    bool ok = Urn.TryParse("not-a-urn", null, out _);
    Console.WriteLine($"Geçersiz parse: {ok} (beklened: False)");

    ok = Urn.TryParse("urn:ns:", null, out _); // boş nss
    Console.WriteLine($"Boş NSS: {ok} (beklened: False)");

    // Eşitlik
    Urn same = Urn.Parse(userUrn.Value);
    Console.WriteLine($"\nEşitlik: {userUrn == same}"); // True

    // Implicit cast
    string raw = userUrn;
    Console.WriteLine($"Implicit string: {raw}");

    // TryFormat
    Span<char> buf = stackalloc char[64];
    userUrn.TryFormat(buf, out int written, default, null);
    Console.WriteLine($"TryFormat: {new string(buf[..written])}");

    // JSON
    string json = JsonSerializer.Serialize(userUrn, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
    Urn fromJson = JsonSerializer.Deserialize<Urn>(json, jsonOpts);
    Console.WriteLine($"JSON roundtrip: {fromJson == userUrn}");
}

// =============================================================================
// PERCENTAGE
// =============================================================================
void RunPercentageExamples() {

    // Sabitler
    Console.WriteLine($"Zero: {Percentage.Zero}");   // 0%
    Console.WriteLine($"Half: {Percentage.Half}");   // 50%
    Console.WriteLine($"Full: {Percentage.Full}");   // 100%

    // Oluşturma
    Percentage p25 = Percentage.FromInt(25);          // 25%
    Percentage p50 = Percentage.FromDouble(0.5);      // 50%
    Percentage p75 = Percentage.FromInt(75);          // 75%
    Console.WriteLine($"\n25%: {p25}");
    Console.WriteLine($"50%: {p50}");
    Console.WriteLine($"75%: {p75}");

    // Clamped aritmetik — hiçbir zaman sınır dışına çıkmaz
    Percentage sum = p75.AddClamped(p50);       // 75+50 = 125 → 100% (clamped)
    Percentage diff = p25.SubtractClamped(p50); // 25-50 = -25 → 0% (clamped)
    Console.WriteLine($"\n75+50 (clamped): {sum}");  // 100%
    Console.WriteLine($"25-50 (clamped): {diff}");   // 0%

    // ApplyTo
    double discounted = p25.ApplyTo(1000.0); // 25% of 1000 = 250
    Console.WriteLine($"\n25% of 1000: {discounted}");

    // Remaining
    Percentage remaining = p25.Remaining; // 100% - 25% = 75%
    Console.WriteLine($"Remaining: {remaining}"); // 75%

    // Operatörler
    double result = p25 * 200.0;             // 50.0
    TimeSpan ts = p50 * TimeSpan.FromHours(8); // 4 saat
    Console.WriteLine($"\n25% * 200 = {result}");
    Console.WriteLine($"50% * 8h = {ts.TotalHours}h");

    Percentage product = p25 * p50;          // 0.25 * 0.5 = 0.125 = 12.5%
    Console.WriteLine($"25% * 50% = {product}");

    // Karşılaştırma
    Console.WriteLine($"\n25% < 50%: {p25 < p50}");       // True
    Console.WriteLine($"75% > 50%: {p75 > p50}");          // True
    Console.WriteLine($"50% == 0.5: {p50 == 0.5}");        // True (double comparison)

    // Implicit cast to double
    double raw = p25;
    Console.WriteLine($"\nImplicit double: {raw}"); // 0.25

    // JSON
    string json = JsonSerializer.Serialize(p25, jsonOpts);
    Console.WriteLine($"\nJSON: {json}");
    Percentage fromJson = JsonSerializer.Deserialize<Percentage>(json, jsonOpts);
    Console.WriteLine($"JSON'dan: {fromJson}");

    // Hata senaryoları
    try { Percentage.FromInt(-1); }
    catch(ArgumentOutOfRangeException e) { Console.WriteLine($"\n-1% → {e.GetType().Name} ✓"); }

    try { Percentage.FromDouble(1.5); }
    catch(ArgumentOutOfRangeException e) { Console.WriteLine($"1.5 → {e.GetType().Name} ✓"); }
}

// =============================================================================
// SECRET<T>
// =============================================================================
void RunSecretExamples() {

    // String'den secret oluşturma
    using Secret<byte> apiKey = Secret.From("super-secret-api-key-12345");
    Console.WriteLine($"Secret length: {apiKey.Length}");

    // Cryptographically random bytes
    using Secret<byte> masterKey = Secret.Generate(32);
    Console.WriteLine($"Master key length: {masterKey.Length}");

    // Expose pattern — veri sadece callback içinde erişilebilir
    apiKey.Expose(span => {
        Console.WriteLine($"Key prefix: 0x{span[0]:X2} 0x{span[1]:X2}...");
    });

    // Expose ile return değeri
    int keyLength = apiKey.Expose(span => span.Length);
    Console.WriteLine($"Key length via Expose: {keyLength}");

    // HMAC hesaplama — secret leak'siz
    HmacSha256Hash mac = HmacSha256Hash.Compute(masterKey, [1, 2, 3, 4, 5]);
    Console.WriteLine($"\nHMAC: {mac}");

    // SHA256 hesaplama — secret ile
    Sha256Hash hash = Sha256Hash.Compute(masterKey);
    Console.WriteLine($"SHA256: {hash}");

    // HKDF ile key türetme — yeni secret üretir
    using Secret<byte> derivedKey = masterKey.DeriveKey(salt: apiKey, outputByteCount: 64);
    Console.WriteLine($"\nDerived key length: {derivedKey.Length}");

    Console.WriteLine("Secret dispose edildiğinde bellek sıfırlanır ✓");
}

// =============================================================================
// SNOWFLAKE ID (GELİŞMİŞ)
// =============================================================================
void RunSnowflakeAdvancedExamples() {

    SnowflakeId.Configure(nodeId: 1);

    // Toplu üretim
    var ids = Enumerable.Range(0, 10).Select(_ => SnowflakeId.NewId()).ToList();
    Console.WriteLine($"10 ID üretildi, hepsi sıralı mı: {ids.Zip(ids.Skip(1)).All(p => p.First < p.Second)}");

    SnowflakeId id = SnowflakeId.NewId();

    // Format seçenekleri
    Console.WriteLine($"\nDefault: {id}");
    Console.WriteLine($"Hex: {id.ToHexString()}");
    Console.WriteLine($"Base62: {id.ToBase62String()}");

    // GuidV7 ile karşılaştırma
    GuidV7 guidId = GuidV7.Create();
    Console.WriteLine($"\nSnowflake (19 char): {id}");
    Console.WriteLine($"GuidV7 D (36 char): {guidId}");
    Console.WriteLine($"GuidV7 N (32 char): {guidId.ToString("N")}");
    Console.WriteLine($"GuidV7 B64Url (22 char): {guidId.ToBase64Url()}");
}

// =============================================================================
// GERÇEK HAYAT SENARYOLARI
// =============================================================================
void RunRealWorldScenarios() {

    Console.WriteLine("─── Senaryo 1: IAM Kullanıcı Kaydı ───");
    {
        // Tip sistemi yanlış kullanımı compile-time'da engeller
        var user = new IamUser(
            Id: GuidV7.Create(),
            Email: NonEmptyString.Create("alice@acme.com"),
            Username: BoundedString<C3, C50>.Create("alice"),
            Role: NonEmptyString.Create("org:member")
        );
        Console.WriteLine($"User: {user.Id} / {user.Username} ({user.Email})");

        // JSON serialization
        string json = JsonSerializer.Serialize(user, jsonOpts);
        Console.WriteLine($"JSON:\n{json}");
    }

    Console.WriteLine("\n─── Senaryo 2: Feature Flag Rule ───");
    {
        // Rollout yüzdesi — tip garantisi ile
        Percentage rollout = Percentage.FromInt(25);
        Console.WriteLine($"Rollout: {rollout} kullanıcıya aktif");

        // Kullanıcı ID'si bu rollout'a dahil mi?
        GuidV7 userId = GuidV7.Create();
        // Basit hash-based rollout kararı
        bool inRollout = (Math.Abs(userId.Value.GetHashCode()) % 100) < (int)(rollout.Value * 100);
        Console.WriteLine($"User {userId.ToString()[..8]}... rollout'ta mı: {inRollout}");
    }

    Console.WriteLine("\n─── Senaryo 3: API Rate Limit Penceresi ───");
    {
        // [şimdi, şimdi+1 dakika) yarı açık pencere
        UnixTimestamp windowStart = UnixTimestamp.Now;
        UnixTimestamp windowEnd = windowStart + TimeSpan.FromMinutes(1);
        Interval<UnixTimestamp> window = Interval<UnixTimestamp>.ClosedOpen(windowStart, windowEnd);

        Console.WriteLine($"Rate limit penceresi: {window}");

        UnixTimestamp requestTime = UnixTimestamp.Now;
        Console.WriteLine($"İstek pencerede mi: {window.Contains(requestTime)}");

        PositiveInt maxRequests = PositiveInt.Create(100);
        NonNegativeInt currentCount = NonNegativeInt.Create(57);
        Console.WriteLine($"Kota: {currentCount}/{maxRequests}");
        Console.WriteLine($"Kalan: {maxRequests.Value - currentCount.Value}");
    }

    Console.WriteLine("\n─── Senaryo 4: Dosya Upload ───");
    {
        MimeType contentType = MimeType.ImagePng;
        PositiveInt maxSizeMb = PositiveInt.Create(10);

        string validationResult = contentType switch {
            _ when contentType.IsImage => $"Görsel kabul edildi ({contentType}), max {maxSizeMb}MB",
            _ when contentType.IsVideo => $"Video kabul edildi ({contentType}), max {maxSizeMb}MB",
            _ => $"Desteklenmeyen tip: {contentType}"
        };
        Console.WriteLine(validationResult);
    }

    Console.WriteLine("\n─── Senaryo 5: Audit Log Entry ───");
    {
        var entry = new AuditEntry(
            Id: GuidV7.Create(),
            UserId: Urn.Create("user", GuidV7.Create().ToString()),
            Action: NonEmptyString.Create("auth.login.success"),
            IpAddress: NonEmptyString.Create("195.142.0.1"),
            Timestamp: UnixTimestamp.Now
        );

        Console.WriteLine($"Audit: [{entry.Timestamp}] {entry.UserId.Namespace.ToString()}:{entry.UserId.Identity.ToString()[..8]}... → {entry.Action}");
    }

    Console.WriteLine("\n─── Senaryo 6: TOTP Secret ───");
    {
        // 20 byte random secret → Base32 (authenticator app formatı)
        using Secret<byte> totpSecret = Secret.Generate(20);
        Base32String base32Key = totpSecret.Expose(span => Base32String.FromBytes(span));
        Console.WriteLine($"TOTP Key: {base32Key}");
        Console.WriteLine($"Key length: {base32Key.Value.Length} chars (standart: 32)");

        // Authenticator URL
        NonEmptyString issuer = NonEmptyString.Create("MyApp");
        NonEmptyString account = NonEmptyString.Create("alice@myapp.com");
        string otpAuthUrl = $"otpauth://totp/{issuer}:{account}?secret={base32Key}&issuer={issuer}";
        Console.WriteLine($"OTP Auth URL: {otpAuthUrl}");
    } 
}

// =============================================================================
// YARDIMCI METOTLAR
// =============================================================================
void PrintHeader(string title) {
    Console.WriteLine();
    Console.WriteLine($"╔══════════════════════════════════════════╗");
    Console.WriteLine($"║  {title,-40}║");
    Console.WriteLine($"╚══════════════════════════════════════════╝");
}

// =============================================================================
// YARDIMCI TİPLER
// =============================================================================
record IamUser(
    GuidV7 Id,
    NonEmptyString Email,
    BoundedString<C3, C50> Username,
    NonEmptyString Role
);

record AuditEntry(
    GuidV7 Id,
    Urn UserId,
    NonEmptyString Action,
    NonEmptyString IpAddress,
    UnixTimestamp Timestamp
);

sealed class FixedTimeProvider(DateTimeOffset time) : TimeProvider {
    public override DateTimeOffset GetUtcNow() => time;
}

