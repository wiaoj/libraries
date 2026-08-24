//using Wiaoj.Security;
//using Wiaoj.Security.Testing;
//using Wiaoj.Webhooks;
//using Wiaoj.Webhooks.AspNetCore; 

//WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//builder.Logging.ClearProviders();
//builder.Logging.AddSimpleConsole(options => {
//    options.SingleLine = true;
//    options.TimestampFormat = "HH:mm:ss.fff ";
//});

//// 1. DI Kayıtları
//builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>, FakeSecretProtector<WebhookSigningContext>>();
//builder.Services.AddSingleton<InMemorySampleEndpointResolver>();
//builder.Services.AddSingleton<IWebhookEndpointResolver>(sp => sp.GetRequiredService<InMemorySampleEndpointResolver>());

//// 2. Gelen Webhook Tüketici Handler Kaydı (Inbound Handler)
//builder.Services.AddScoped<IWebhookReceiverHandler<OrderCreatedWebhookEvent>, OrderCreatedReceiverHandler>();

//// 3. Outbound Transport & Webhooks Engine
//builder.Services.AddInMemoryWebhookTransport(options => {
//    options.Concurrency = 4;
//});

//builder.Services.AddWiaojWebhooks(options => {
//    options
//           //.UseHmacSha256Signing()
//           //.UsePartitionedDelivery()
//           //.UseIdempotency()
//           //.UseStandardHeaders()
//           .AllowPrivateNetworks()
//           //.UseExponentialBackoffRetry()
//           .RegisterEvent<OrderCreatedWebhookEvent>("order.created");
//});

//WebApplication app = builder.Build();

//const string sharedSecret = "whsec_super_secure_sample_key_1234567890_32bytes!";

//// 🌟 4. Gelen Webhook Endpoint'i (INBOUND RECEIVER)
////app.MapWebhookReceiver<OrderCreatedWebhookEvent>(
////    pattern: "/api/webhooks/orders",
////    secret: sharedSecret
////);
 
//app.MapWebhook<OrderCreatedWebhookEvent>(
//    pattern: "/api/webhooks/orders",
//    secret: sharedSecret,
//    handler: async (OrderCreatedWebhookEvent order) => {
//        // DoS korumalı, HMAC imzası doğrulanmış ve Idempotency'den geçmiş temiz veri:
//        Console.WriteLine($"🎉 [MINIMAL API HANDLER] Sipariş Alındı: {order.OrderId}, Tutar: {order.TotalAmount}");
//        await Task.CompletedTask;
//    }
//);

//// Web sunucusunu arka planda başlat
//_ = app.RunAsync("http://127.0.0.1:5200");

//// 5. Giden Webhook Ayarları (OUTBOUND)
//IWebhookDispatcher dispatcher = app.Services.GetRequiredService<IWebhookDispatcher>();
//InMemorySampleEndpointResolver resolver = app.Services.GetRequiredService<InMemorySampleEndpointResolver>();
//ISecretProtector<WebhookSigningContext> protector = app.Services.GetRequiredService<ISecretProtector<WebhookSigningContext>>();

//WebhookEndpointId selfEndpointId = new("local-receiver");
//Uri selfTargetUrl = new("http://127.0.0.1:5200/api/webhooks/orders");
//resolver.Register(new WebhookEndpoint(selfEndpointId, selfTargetUrl, protector.Protect(sharedSecret)));

//Console.WriteLine("===================================================================");
//Console.WriteLine("🚀 Wiaoj Webhooks End-to-End Test (Outbound -> Inbound)");
//Console.WriteLine($"📡 Listening Inbound Webhooks on: {selfTargetUrl}");
//Console.WriteLine("===================================================================\n");

//Console.WriteLine("Kendi Inbound endpoint'imize Webhook göndermek için [ENTER]'a basın...");
//Console.ReadLine();

//// ── 1. Webhook Fırlat ──
//OrderCreatedWebhookEvent sampleOrder = new("ORD-2026-99", "cust_10", 250.00m, "USD");
//Console.WriteLine("⚡ Dispatching webhook to local endpoint...");
//WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(selfEndpointId, sampleOrder);
//Console.WriteLine($"✅ Dispatched with Job ID: {handle.JobId}");

//// ── 2. Mükerrer Gönderim Testi ──
//Console.WriteLine("\n[ENTER]'a basarak AYNI event'i tekrar gönderin (Idempotency yakalayacak)...");
//Console.ReadLine();

//await dispatcher.DispatchAsync(selfEndpointId, sampleOrder);

//await Task.Delay(2000);
//Console.WriteLine("\nÇıkmak için [ENTER]'a basın...");
//Console.ReadLine();

//// ── Event Modeli ──
//[WebhookEvent("order.created")]
//public sealed record OrderCreatedWebhookEvent(
//    string OrderId,
//    string CustomerId,
//    decimal TotalAmount,
//    string Currency) : IWebhookEvent;

//// ── Gelen Webhook'u Karşılayıp İşleyen Sınıf ──
//public sealed class OrderCreatedReceiverHandler(ILogger<OrderCreatedReceiverHandler> logger)
//    : IWebhookReceiverHandler<OrderCreatedWebhookEvent> {

//    public Task HandleAsync(WebhookReceiverContext<OrderCreatedWebhookEvent> context, CancellationToken cancellationToken = default) {
//        logger.LogInformation(
//            "🎉 [INBOUND HANDLER EXECUTED] Successfully processed Order ID: {OrderId} for Customer: {CustomerId}. Total: {Amount} {Currency}",
//            context.Payload.OrderId,
//            context.Payload.CustomerId,
//            context.Payload.TotalAmount,
//            context.Payload.Currency);

//        return Task.CompletedTask;
//    }
//}

//// ── Endpoint Rehberi ──
//public sealed class InMemorySampleEndpointResolver : IWebhookEndpointResolver {
//    private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new(WebhookEndpointId.OrdinalComparer);
//    public void Register(WebhookEndpoint endpoint) {
//        this._endpoints[endpoint.Id] = endpoint;
//    }

//    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
//        this._endpoints.TryGetValue(endpointId, out WebhookEndpoint? ep);
//        return ValueTask.FromResult(ep);
//    }
//}