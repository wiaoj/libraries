//using Wiaoj.Primitives.Cryptography.Symmetric;
//using Wiaoj.Security;
//using Wiaoj.Security.Testing;
//using Wiaoj.Serialization;
//using Wiaoj.Serialization.DependencyInjection;
//using Wiaoj.Webhooks;


//IHost host = Host.CreateDefaultBuilder(args)
//    .ConfigureLogging(logging => {
//        logging.ClearProviders();
//        logging.AddSimpleConsole(options => {
//            options.IncludeScopes = false;
//            options.SingleLine = true;
//            options.TimestampFormat = "HH:mm:ss.fff ";
//        });
//        logging.SetMinimumLevel(LogLevel.Debug);
//    })
//    .ConfigureServices((context, services) => {
//        // 1. Secret Protection & In-Memory Endpoint Resolver
//        services.AddSingleton<ISecretProtector<WebhookSigningContext>, FakeSecretProtector<WebhookSigningContext>>();
//        services.AddSingleton<InMemorySampleEndpointResolver>();
//        services.AddSingleton<IWebhookEndpointResolver>(sp => sp.GetRequiredService<InMemorySampleEndpointResolver>());

//        // 2. In-Memory Transport with Multi-Worker Consumer
//        services.AddInMemoryWebhookTransport(options => {
//            options.Concurrency = 4;
//            options.Capacity = 1000;
//        });


//        services.AddWiaojSerializer(serializer => {
//            serializer.UseYamlDotNet<WebhookSerializerKey>();
//        });



//        // 3. Wiaoj Webhooks Core Engine Pipeline
//        services.AddWiaojWebhooks(builder => {
//            builder.UseHmacSha256Signing()        // Kriptografik İmzalama
//                   .UsePartitionedDelivery()       // Zero-Collision Mailbox Kilidi
//                   .UseIdempotency()               // XxHash128 SIMD Deduplication
//                   .UseExponentialBackoffRetry()   // Jitter'lı Üstel Retry
//                   .UseStandardHeaders()
//                   .UseContentDigest()
//                   .RegisterEvent<OrderCreatedWebhookEvent>("order.created");
//        });
//    })
//    .Build();

//// Arka plan worker'larını başlat
//await host.StartAsync();

//// Servisleri çöz
//IWebhookDispatcher dispatcher = host.Services.GetRequiredService<IWebhookDispatcher>();
//InMemorySampleEndpointResolver resolver = host.Services.GetRequiredService<InMemorySampleEndpointResolver>();
//ISecretProtector<WebhookSigningContext> protector = host.Services.GetRequiredService<ISecretProtector<WebhookSigningContext>>();

//// 🌟 CANLI HEDEF: RequestCatcher Endpoint'ini Kaydet
//WebhookEndpointId endpointId = new("request-catcher-live");
//Uri targetUrl = new("https://wiaoj.requestcatcher.com/test-endpoint");
//EncryptedSecret<WebhookSigningContext> secret = protector.Protect("whsec_live_secret_key_sample_32bytes_long!");

//resolver.Register(new WebhookEndpoint(endpointId, targetUrl, secret));

//Console.WriteLine($"\n📡 Target registered: {targetUrl}");
//Console.WriteLine("👉 Lütfen tarayıcınızda şu adresi açın: https://wiaoj.requestcatcher.com/\n");
//Console.WriteLine("Event fırlatmak için [ENTER]'a basın...");
//Console.ReadLine();

//// ── 1. CANLI WEBHOOK GÖNDERİMİ ──
//OrderCreatedWebhookEvent sampleEvent = new(
//   OrderId: "ORD-2026-9941",
//   CustomerId: "cust_8841",
//   TotalAmount: 149.99m,
//   Currency: "USD");

//Console.WriteLine("⚡ Dispatching event 'order.created' to RequestCatcher...");
//WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(endpointId, sampleEvent);
//Console.WriteLine($"✅ Event queued with Job ID: {handle.JobId}");

//// ── 2. MÜKERRER (DUPLICATE) TESTİ ──
//Console.WriteLine("\n[ENTER]'a basarak AYNI event'i tekrar gönderip Idempotency Middleware'in yakalamasını izleyin...");
//Console.ReadLine();

//Console.WriteLine("⚡ Dispatching DUPLICATE event...");
//WebhookDeliveryHandle duplicateHandle = await dispatcher.DispatchAsync(endpointId, sampleEvent);
//Console.WriteLine($"✅ Event queued with Job ID: {duplicateHandle.JobId} (İzleyin: RequestCatcher'a 2. istek gitmeyecek, yutulacak!)");

//// Worker'ların isteği göndermesi için kısa bekleme
//await Task.Delay(3000);

//Console.WriteLine("\nÇıkmak için [ENTER]'a basın...");
//Console.ReadLine();

//await host.StopAsync();

//// ── Sample Event Model ──
//[WebhookEvent("order.created")]
//public sealed record OrderCreatedWebhookEvent(
//    string OrderId,
//    string CustomerId,
//    decimal TotalAmount,
//    string Currency) : IWebhookEvent;

//// ── In-Memory Sample Endpoint Directory ──
//public sealed class InMemorySampleEndpointResolver : IWebhookEndpointResolver {
//    private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new(WebhookEndpointId.OrdinalComparer);

//    public void Register(WebhookEndpoint endpoint) {
//        this._endpoints[endpoint.Id] = endpoint;
//    }

//    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
//        this._endpoints.TryGetValue(endpointId, out WebhookEndpoint? endpoint);
//        return ValueTask.FromResult(endpoint);
//    }
//}