using System.Text;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting.AspNetCore;
using Wiaoj.RateLimiting.AspNetCore.KeySelectors;

Console.OutputEncoding = Encoding.UTF8;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. Logging Ayarları
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opt => {
    opt.SingleLine = true;
    opt.TimestampFormat = "[HH:mm:ss.fff] ";
});
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// 2. DistributedCounter (In-Memory) ve RateLimiter Kaydı (Limit: 3 istek / 3 saniye)
builder.Services.AddDistributedCounter(b => b.UseInMemory());
builder.Services.AddWiaojRateLimiting(rl => {
    rl.UseFixedWindow(limit: 3, window: TimeSpan.FromSeconds(3));
});

WebApplication app = builder.Build();

// 3. RateLimiting Middleware'ini Devreye Al (IP bazlı, ProblemDetails aktif)
app.UseWiaojRateLimiting(options => {
    options.KeySelector = new ClientIpKeySelector(prefix: "api_client_ip:");
    options.UseProblemDetails = true;
});

// 4. Test Endpoint'i
app.MapGet("/api/orders", () => Results.Ok(new {
    message = "Sipariş listesi başarıyla getirildi.",
    timestamp = DateTimeOffset.UtcNow
}));

// API'yi arka planda localhost:5050 üzerinden başlat
_ = app.RunAsync("http://localhost:5050");

// --------------------------------------------------------------------------------
// 5. CANLI HTTP TEST İSTEMCİSİ (Otomatik Olarak API'yi Test Eder)
// --------------------------------------------------------------------------------
await Task.Delay(1000); // API'nin ayağa kalkması için 1 saniye bekle

Console.WriteLine("\n==================================================================");
Console.WriteLine("🌐 Canlı HTTP API Testi Başlıyor (Hedef: http://localhost:5050/api/orders)");
Console.WriteLine("🎯 Limit: 3 İstek / 3 Saniye");
Console.WriteLine("==================================================================\n");

using HttpClient client = new() { BaseAddress = new Uri("http://localhost:5050") };

for(int i = 1; i <= 5; i++) {
    Console.WriteLine($"\n➡️ [HTTP İSTEK #{i}] Gönderiliyor...");

    HttpResponseMessage response = await client.GetAsync("/api/orders");
    string body = await response.Content.ReadAsStringAsync();

    if(response.IsSuccessStatusCode) {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✅ HTTP {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine($"   Headers: RateLimit-Remaining = {response.Headers.GetValues("RateLimit-Remaining").FirstOrDefault()}");
        Console.WriteLine($"   Body: {body}");
        Console.ResetColor();
    }
    else {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"🚫 HTTP {(int)response.StatusCode} {response.StatusCode} (LIMIT AŞILDI!)");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"   Headers: Retry-After = {response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0}s");
        Console.WriteLine($"   Headers: RateLimit-Reset = {response.Headers.GetValues("RateLimit-Reset").FirstOrDefault()}s");
        Console.WriteLine($"   RFC 7807 ProblemDetails Body:\n{body}");
        Console.ResetColor();
    }
}

Console.WriteLine("\n==================================================================");
Console.WriteLine("⏳ 3.5 Saniye bekleniyor (Pencere sıfırlanacak)...");
await Task.Delay(3500);

Console.WriteLine("\n➡️ [SIFIRLAMA SONRASI İSTEK #6] Gönderiliyor...");
HttpResponseMessage resetResponse = await client.GetAsync("/api/orders");
string resetBody = await resetResponse.Content.ReadAsStringAsync();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"✅ HTTP {(int)resetResponse.StatusCode} {resetResponse.StatusCode} (Pencere Sıfırlandı, İstek Kabul Edildi!)");
Console.WriteLine($"   Headers: RateLimit-Remaining = {resetResponse.Headers.GetValues("RateLimit-Remaining").FirstOrDefault()}");
Console.WriteLine($"   Body: {resetBody}");
Console.ResetColor();

Console.WriteLine("\n==================================================================");
Console.WriteLine("🚀 API 'http://localhost:5050/api/orders' adresinde ÇALIŞMAYA DEVAM EDİYOR.");
Console.WriteLine("Postman veya tarayıcınızdan istek atıp test edebilirsiniz. (Çıkış için Ctrl+C)");
Console.WriteLine("==================================================================");

// Program kapanmasın, API açık kalsın
await Task.Delay(Timeout.Infinite);