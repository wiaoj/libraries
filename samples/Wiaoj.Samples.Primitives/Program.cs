

using Wiaoj.Primitives.Snowflake;

SnowflakeId snowflakeId = SnowflakeId.NewId();
SnowflakeId snowflakeId2 = SnowflakeId.NewId(); 

Console.WriteLine(snowflakeId);
Console.WriteLine(snowflakeId2);

SnowflakeId.Configure(0, DateTimeOffset.UtcNow);

long snowflakeId3 = SnowflakeId.NewId();

Console.WriteLine(snowflakeId3);

// 1. Adım: ID'yi 22 bit sağa kaydırarak milisaniye farkını (offset) bul
long offsetMs = snowflakeId >> 22;

// 2. Adım: Senin belirlediğin Epoch (1 Ocak 2024)
DateTimeOffset epoch = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
long epochMs = epoch.ToUnixTimeMilliseconds();

// 3. Adım: Epoch ile farkı topla ve tarihe çevir
DateTimeOffset gercekZaman = DateTimeOffset.FromUnixTimeMilliseconds(epochMs + offsetMs);

Console.WriteLine($"ID'nin içindeki zaman: {gercekZaman}");

//using System.Diagnostics;
//using Wiaoj.Primitives; // Kütüphanenizin namespace'i

//Console.WriteLine("=== GERÇEK ZAMAN ÖLÇÜMLÜ TEST ===\n");

//// ---------------------------------------------------------
//// SENARYO 1: YANLIŞ KULLANIM (Sürenin Uzaması)
//// Beklenti: Toplam süre 3 saniyeyi geçecek (yaklaşık 4sn) ve HATA VERMEYECEK.
//// ---------------------------------------------------------
//Log("TEST 1 BAŞLIYOR: Yanlış Kullanım (Struct'ı direkt parametre geçmek)", ConsoleColor.Yellow);

//Stopwatch sw1 = Stopwatch.StartNew(); // KRONOMETREYİ BAŞLAT
//try {
//    // Kural: Maksimum 3 saniye sürsün.
//    OperationTimeout timeout = OperationTimeout.FromMilliseconds(TimeSpan.FromSeconds(3));

//    await Controller_Wrong_PassStruct(timeout);

//    sw1.Stop(); // DURDUR

//    // SONUÇ ANALİZİ (Kod karar veriyor)
//    double gecenSure = sw1.Elapsed.TotalSeconds;

//    if(gecenSure > 3.1) // 3 saniyeyi belirgin şekilde aştı mı?
//    {
//        Log($"SONUÇ: BAŞARISIZ! (Ama beklenen buydu)", ConsoleColor.Red);
//        Log($"ÖLÇÜLEN SÜRE: {gecenSure:N2} saniye.", ConsoleColor.Red);
//        Log($"LİMİT: 3.00 saniye idi.", ConsoleColor.Red);
//        Log($"AÇIKLAMA: Hata fırlatılmadı çünkü alt metotta süre sıfırlandı.", ConsoleColor.White);
//    }
//    else {
//        Log($"SONUÇ: İLGİNÇ.. Süre içinde bitti: {gecenSure:N2}s", ConsoleColor.Green);
//    }
//}
//catch(OperationCanceledException) {
//    sw1.Stop();
//    Log($"SONUÇ: Beklenmedik şekilde iptal oldu. Süre: {sw1.Elapsed.TotalSeconds:N2}s", ConsoleColor.Green);
//}

//Console.WriteLine("\n---------------------------------------------------------\n");

//// ---------------------------------------------------------
//// SENARYO 2: DOĞRU KULLANIM (Token Aktarımı)
//// Beklenti: 3. saniye civarında PATLAMALI.
//// ---------------------------------------------------------
//Log("TEST 2 BAŞLIYOR: Doğru Kullanım (Token Geçişi)", ConsoleColor.Cyan);

//Stopwatch sw2 = Stopwatch.StartNew();
//try {
//    OperationTimeout timeout = OperationTimeout.FromMilliseconds(TimeSpan.FromSeconds(3));
//    await Controller_Correct_PassToken(timeout);

//    sw2.Stop();
//    Log($"HATA: İşlem hiç kesilmedi! Süre: {sw2.Elapsed.TotalSeconds:N2}s", ConsoleColor.Red);
//}
//catch(OperationCanceledException) {
//    sw2.Stop();
//    double gecenSure = sw2.Elapsed.TotalSeconds;

//    // Hata fırlatıldı, peki doğru zamanda mı?
//    // 3.0 ile 3.2 saniye arası makul bir gecikmedir.
//    Log($"SONUÇ: BAŞARILI! Hata yakalandı.", ConsoleColor.Green);
//    Log($"ÖLÇÜLEN SÜRE: {gecenSure:N2} saniye.", ConsoleColor.Green);
//    Log($"LİMİT: 3.00 saniye idi. Tam zamanında kesildi.", ConsoleColor.Green);
//}

//Console.WriteLine("\n---------------------------------------------------------\n");

//// ---------------------------------------------------------
//// SENARYO 3: DOĞRU KULLANIM (Wrapper - FromMilliseconds(token))
//// Beklenti: Yine 3. saniyede PATLAMALI.
//// ---------------------------------------------------------
//Log("TEST 3 BAŞLIYOR: Doğru Kullanım (Wrapper - FromMilliseconds(token))", ConsoleColor.Cyan);

//Stopwatch sw3 = Stopwatch.StartNew();
//try {
//    OperationTimeout timeout = OperationTimeout.FromMilliseconds(TimeSpan.FromSeconds(3));
//    await Controller_Correct_PassWrapper(timeout);

//    sw3.Stop();
//    Log($"HATA: İşlem hiç kesilmedi! Süre: {sw3.Elapsed.TotalSeconds:N2}s", ConsoleColor.Red);
//}
//catch(OperationCanceledException) {
//    sw3.Stop();
//    double gecenSure = sw3.Elapsed.TotalSeconds;

//    Log($"SONUÇ: BAŞARILI! Wrapper yöntemi çalıştı.", ConsoleColor.Green);
//    Log($"ÖLÇÜLEN SÜRE: {gecenSure:N2} saniye.", ConsoleColor.Green);
//    Log($"LİMİT: 3.00 saniye idi.", ConsoleColor.Green);
//}

//Console.ReadLine();

//// =========================================================================
//// TEST METOTLARI
//// =========================================================================

//// --- SENARYO 1 (YANLIŞ) ---
//async Task Controller_Wrong_PassStruct(OperationTimeout timeout) {
//    Log($"[Controller] Başladı. 2 saniye bekliyor...");
//    await timeout.ExecuteAsync(async (OperationTimeout activeTimeout) => {
//        await Task.Delay(2000, activeTimeout.Token); // 2 saniye harca

//        Log($"[Controller] Bitti. Service çağrılıyor (Struct aynen veriliyor)...");
//        // HATA: Aynı struct tekrar veriliyor, süre sıfırlanacak.
//        await Service_Wrong_Recalculates(activeTimeout);
//    });
//}

//async Task Service_Wrong_Recalculates(OperationTimeout timeout) {
//    // Burada timeout.ExecuteAsync dediğimizde YENİ bir 3 saniye başlıyor.
//    Log($"[Service] Başladı. Yeni sayaç kuruldu (HATA BURADA). 2 saniye bekliyor...");
//    await timeout.ExecuteAsync(async (token) => {
//        await Task.Delay(2000, token); // 2 saniye daha harca. (Toplam 4 oldu)
//        Log($"[Service] Bitti.");
//    });
//}

//// --- SENARYO 2 (DOĞRU - TOKEN) ---
//async Task Controller_Correct_PassToken(OperationTimeout timeout) {
//    Log($"[Controller] Başladı. 2 saniye bekliyor...");
//    await timeout.ExecuteAsync(async (token) => {
//        await Task.Delay(2000, token);

//        Log($"[Controller] Bitti. Service çağrılıyor (Token veriliyor)...");
//        // DOĞRU: Aktif token veriliyor.
//        await Service_Correct_ReceivesToken(token);
//    });
//}

//async Task Service_Correct_ReceivesToken(CancellationToken token) {
//    Log($"[Service] Başladı. Token ile 2 saniye beklemeye çalışacak...");
//    // Toplamda 3 saniye limit vardı. 2'si gitti. 
//    // Buradaki 2 saniyelik beklemenin 1. saniyesinde token iptal olmalı.
//    await Task.Delay(2000, token);
//    Log($"[Service] Bitti (Bunu görmemen lazım).");
//}


//// --- SENARYO 3 (DOĞRU - WRAPPER) ---
//async Task Controller_Correct_PassWrapper(OperationTimeout timeout) {
//    Log($"[Controller] Başladı. 2 saniye bekliyor...");
//    await timeout.ExecuteAsync(async (token) => {
//        await Task.Delay(2000, token);

//        Log($"[Controller] Bitti. Service çağrılıyor (FromMilliseconds(token) ile sarıldı)...");
//        OperationTimeout childTimeout = OperationTimeout.FromMilliseconds(token);
//        await Service_Correct_ReceivesWrapper(childTimeout);
//    });
//}

//async Task Service_Correct_ReceivesWrapper(OperationTimeout timeout) {
//    Log($"[Service] Başladı. 2 saniye beklemeye çalışacak...");

//    // Gelen timeout içinde sadece Token var, süre Infinite.
//    // ExecuteAsync yeni sayaç başlatmaz, token'ı dinler.
//    await timeout.ExecuteAsync(async (token) => {
//        await Task.Delay(2000, token);
//        Log($"[Service] Bitti (Bunu görmemen lazım).");
//    });
//}

//// --- HELPER ---
//void Log(string message, ConsoleColor color = ConsoleColor.White) {
//    var prevColor = Console.ForegroundColor;
//    Console.ForegroundColor = color;
//    // Log'a da zaman damgası ekliyorum ki konsol akışından da takip et.
//    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
//    Console.ForegroundColor = prevColor;
//}