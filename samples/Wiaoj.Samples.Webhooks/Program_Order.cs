//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Primitives;
//using System.Text;
//using Wiaoj.BloomFilter;
//using Wiaoj.DistributedCounter;
//using Wiaoj.Samples.Webhooks.Infrastructure;
//using Wiaoj.Security;
//using Wiaoj.Security.Testing;
//using Wiaoj.Serialization;
//using Wiaoj.Serialization.SystemTextJson;
//using Wiaoj.Webhooks;
//using Wiaoj.Webhooks.BloomFilter;
//using Wiaoj.Webhooks.Retries;
//using Wiaoj.Webhooks.Security;
//using Wiaoj.Webhooks.Transports.InMemory;

//WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//// 1. Serialization & Security Setup
//builder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
//FakeSecretProtector<WebhookSigningContext> protector = new();
//builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(protector);

//// Localhost 127.0.0.1 testlerine izin vermek için SSRF ayarı (Production'da false olmalıdır)
//builder.Services.Configure<WebhookSecurityOptions>(options => {
//    options.AllowPrivateNetworks = true;
//    options.MaxResponseBodyBytes = 8192;
//});

//// 2. Setup Sample Endpoints
//SampleEndpointStore endpointStore = new();
//const string sampleSecretKey = "whsec_sample_secret_key_32bytes_minimum_length_for_aes_gcm";
//EncryptedSecret<WebhookSigningContext> encryptedSecret = protector.Protect(sampleSecretKey);

//// A. Stable Endpoint (200 OK)
//endpointStore.Register(new WebhookEndpoint(
//    new WebhookEndpointId("acme-corp"),
//    new Uri("http://127.0.0.1:5210/api/webhooks/receiver"),
//    encryptedSecret));

//// B. Flaky Endpoint (2x 503 Service Unavailable -> 3rd attempt 200 OK)
//endpointStore.Register(new WebhookEndpoint(
//    new WebhookEndpointId("flaky-corp"),
//    new Uri("http://127.0.0.1:5210/api/webhooks/flaky-receiver"),
//    encryptedSecret));

//// C. Terminal Failing Endpoint (400 Bad Request -> Immediate DeadLetter)
//endpointStore.Register(new WebhookEndpoint(
//    new WebhookEndpointId("broken-corp"),
//    new Uri("http://127.0.0.1:5210/api/webhooks/terminal-receiver"),
//    encryptedSecret));

//builder.Services.AddSingleton<IWebhookEndpointResolver>(endpointStore);

//// 3. Bloom Filter & Distributed Counter Setup
//builder.Services.AddBloomFilter(bf => {
//    bf.AddFilter("webhook-dedup", expectedItems: 100_000, errorRate: 0.001);
//});

//builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

//// 4. Unified Webhooks Engine Configuration
//builder.Services.AddWiaojWebhooks(webhooks => {
//    webhooks.UseInMemoryTransport()
//            .UseStripedPartitionedDelivery(4096)
//            .UseBloomFilterDeduplication("webhook-dedup", new BloomFilterDeduplicationOptions {
//                KeySelector = ctx => $"{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload}"
//            })
//            .UseDistributedRateLimiting(maxRequestsPerWindow: 5, window: TimeSpan.FromSeconds(3))
//            .UseHmacSha256Signing()
//            .UseExponentialBackoffRetry(new ExponentialBackoffOptions {
//                MaxAttempts = 3,
//                InitialDelay = TimeSpan.FromSeconds(2),
//                Multiplier = 2.0,
//                Jitter = null // Testlerde deterministik süreler için
//            });
//});

//WebApplication app = builder.Build();

//// ── ROOT DASHBOARD & INFO ───────────────────────────────────────────────────
//app.MapGet("/", () => Results.Ok(new {
//    Engine = "Wiaoj Distributed Webhooks Engine v1.0",
//    Status = "Running",
//    Endpoints = new[] {
//        "acme-corp (Stable)",
//        "flaky-corp (Transient Failures / Retry)",
//        "broken-corp (Permanent Failure / Dead-Letter)"
//    }
//}));

//// ── DISPATCH API ────────────────────────────────────────────────────────────

//// Standard Dispatch: Fast-path delivery
//app.MapPost("/api/orders/checkout", async (
//    [FromQuery] string? endpointId,
//    [FromServices] IWebhookDispatcher dispatcher) => {
//        string target = string.IsNullOrWhiteSpace(endpointId) ? "acme-corp" : endpointId;
//        string orderId = $"ORD-{Random.Shared.Next(1000, 9999)}";
//        decimal amount = Math.Round((decimal)(Random.Shared.NextDouble() * 500 + 10), 2);

//        OrderCreatedEvent @event = new(orderId, amount, DateTimeOffset.UtcNow);
//        WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(new WebhookEndpointId(target), @event);

//        return Results.Accepted(value: new {
//            Message = "Webhook dispatched (Store-First -> In-Memory Fast-Path Accepted)!",
//            JobId = handle.JobId.Value,
//            TargetEndpoint = target,
//            Event = @event
//        });
//    });

//// Fixed Dispatch: For testing BloomFilter duplicate suppression
//app.MapPost("/api/orders/checkout-duplicate", async (
//    [FromQuery] string? orderId,
//    [FromQuery] string? endpointId,
//    [FromServices] IWebhookDispatcher dispatcher) => {
//        string target = string.IsNullOrWhiteSpace(endpointId) ? "acme-corp" : endpointId;
//        string resolvedOrderId = string.IsNullOrWhiteSpace(orderId) ? "ORD-STATIC-100" : orderId;

//        OrderCreatedEvent @event = new(resolvedOrderId, 99.99m, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
//        WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(new WebhookEndpointId(target), @event);

//        return Results.Accepted(value: new {
//            Message = "Fixed order dispatched! Sending again with same orderId will trigger BloomFilter deduplication.",
//            JobId = handle.JobId.Value,
//            TargetEndpoint = target,
//            Event = @event
//        });
//    });

//// ── STORE & DEAD-LETTER QUERY API ───────────────────────────────────────────

//// Query specific job status & attempt history
//app.MapGet("/api/webhooks/jobs/{jobId}", async (
//    string jobId,
//    [FromServices] IWebhookStore store) => {
//        WebhookJobRecord? job = await store.GetJobAsync(new WebhookJobId(jobId));
//        return job is not null ? Results.Ok(job) : Results.NotFound(new { Message = $"Job '{jobId}' not found." });
//    });

//// Query all dead-lettered jobs
//app.MapGet("/api/webhooks/dead-letters", async (
//    [FromServices] IWebhookStore store) => {
//        IReadOnlyList<WebhookJobRecord> deadLetters = await store.GetDeadLetteredJobsAsync(50);
//        return Results.Ok(new {
//            TotalCount = deadLetters.Count,
//            DeadLetters = deadLetters
//        });
//    });

//// Query endpoint audit history
//app.MapGet("/api/webhooks/endpoints/{endpointId}/history", async (
//    string endpointId,
//    [FromServices] IWebhookStore store) => {
//        IReadOnlyList<WebhookJobRecord> history = await store.GetHistoryByEndpointAsync(new WebhookEndpointId(endpointId));
//        return Results.Ok(new {
//            EndpointId = endpointId,
//            TotalCount = history.Count,
//            Jobs = history
//        });
//    });

//// Trigger Manual Replay for a failed/dead-lettered job
//app.MapPost("/api/webhooks/jobs/{jobId}/replay", async (
//    string jobId,
//    [FromServices] IWebhookDispatcher dispatcher) => {
//        WebhookDeliveryHandle handle = await dispatcher.ReplayAsync(new WebhookJobId(jobId));
//        return Results.Accepted(value: new {
//            Message = "Job successfully re-enqueued for immediate replay!",
//            JobId = handle.JobId.Value
//        });
//    });

//// ── TARGET WEBHOOK RECEIVERS ────────────────────────────────────────────────

//// 1. Stable Receiver: Verifies HMAC signature and accepts payload
//app.MapPost("/api/webhooks/receiver", async (
//    HttpRequest request,
//    [FromServices] IWebhookSigner signer,
//    [FromServices] ILogger<Program> logger) => {
//        if(!request.Headers.TryGetValue(signer.HeaderName, out StringValues signatureHeader)) {
//            logger.LogWarning("Rejected incoming webhook: missing '{Header}' header.", signer.HeaderName);
//            return Results.Unauthorized();
//        }

//        using StreamReader reader = new(request.Body, Encoding.UTF8);
//        string payload = await reader.ReadToEndAsync();
//        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
//        byte[] secretBytes = Encoding.UTF8.GetBytes(sampleSecretKey);

//        bool isValid = signer.Verify(payloadBytes, signatureHeader.ToString(), secretBytes, TimeSpan.FromMinutes(5));
//        if(!isValid) {
//            logger.LogWarning("Rejected incoming webhook: invalid HMAC signature.");
//            return Results.Unauthorized();
//        }

//        logger.LogInformation("Incoming webhook authenticated successfully: {Payload}", payload);
//        return Results.Ok(new { Status = "Received & Verified", Signature = signatureHeader.ToString() });
//    });

//// 2. Flaky Receiver: Fails 2 times with 503, then recovers on 3rd attempt
//int flakyCallCount = 0;
//app.MapPost("/api/webhooks/flaky-receiver", (
//    [FromServices] ILogger<Program> logger) => {
//        int count = Interlocked.Increment(ref flakyCallCount);
//        if(count % 3 != 0) {
//            logger.LogWarning("Flaky Receiver simulated HTTP 503 on attempt #{Count}. Retrying soon...", count);
//            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
//        }

//        logger.LogInformation("Flaky Receiver recovered on attempt #{Count}! Webhook delivered.", count);
//        return Results.Ok(new { Status = "Recovered & Delivered", Attempt = count });
//    });

//// 3. Terminal Failing Receiver: Always returns 400 Bad Request
//app.MapPost("/api/webhooks/terminal-receiver", (
//    [FromServices] ILogger<Program> logger) => {
//        logger.LogError("Terminal Receiver returned HTTP 400 Bad Request. Direct Dead-Letter triggered.");
//        return Results.BadRequest(new { Error = "Malformed or unacceptable webhook format." });
//    });

//app.Run("http://127.0.0.1:5210");

//// ── SAMPLE EVENT DEFINITION ─────────────────────────────────────────────────
//public sealed record OrderCreatedEvent(string OrderId, decimal Amount, DateTimeOffset CreatedAt) : IWebhookEvent {
//    public static string EventName => "order.created";
//}