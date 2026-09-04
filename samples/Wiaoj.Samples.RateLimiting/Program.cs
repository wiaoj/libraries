using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. Core Distributed Counter Infrastructure (In-Memory or Redis)
builder.Services.AddDistributedCounter(counter => {
    counter.UseInMemory(); // Local test için In-Memory (İsterseniz .UseRedis("localhost:6379") yapabilirsiniz)
});

// 2. Wiaoj Rate Limiting Setup with Negative Caching & Fail-Open
builder.Services.AddWiaojRateLimiting(rl => {
    rl.UseDefaultPolicy(policy => {
        // 10 saniyede en fazla 5 istek (Fixed Window)
        policy.UseFixedWindow(limit: 5, window: TimeSpan.FromSeconds(10));

        // L1 RAM DDoS kalkanı (Spam istekleri Redis'e gitmeden RAM'de anında keser)
        policy.WithNegativeCaching();

        // Redis/Storage çökse bile API'yi çökertmeyip istekleri geçiren sigorta
        policy.WithFailOpen();
    });
});

WebApplication app = builder.Build();

// 3. Rate Limiting Middleware'ini Pipeline'a ekliyoruz
app.UseWiaojRateLimiting();

// --------------------------------------------------------------------------
// CANLI TEST ENDPOINT'LERİ
// --------------------------------------------------------------------------

// Senaryo 1: Standart Korunan Endpoint (10 saniyede max 5 istek)
app.MapGet("/api/standard", () => Results.Ok(new {
    Status = "Success",
    Message = "You are within rate limits!",
    Timestamp = DateTimeOffset.UtcNow
}));

// Senaryo 2: Ağır/Toplu İşlem (Dinamik Cost - Query'den gelen adet kadar kota düşer)
// Örnek: /api/bulk-import?count=3 çağrılırsa kotadan 3 birim birden düşer!
app.MapPost("/api/bulk-import", ([FromQuery] int count) => Results.Ok(new {
    Status = "Success",
    ItemsProcessed = count,
    Message = $"{count} quota units consumed."
})).WithMetadata(new RateLimitMetadata {
    DynamicCostResolver = ctx => {
        return ctx.Request.Query.TryGetValue("count", out StringValues val) && int.TryParse(val, out int c) ? c : 1;
    }
});

// Senaryo 3: Rate Limiter'dan Muaf Endpoint (Healthcheck / Ping)
app.MapGet("/api/health", () => Results.Ok(new {
    Status = "Healthy",
    RateLimiting = "Bypassed"
})).WithMetadata(new DisableRateLimitingAttribute());

app.Run();