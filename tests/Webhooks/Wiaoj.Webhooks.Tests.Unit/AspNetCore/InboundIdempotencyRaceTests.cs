using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore;

public sealed class InboundIdempotencyRaceTests {
    [Fact]
    public async Task InvokeAsync_WhenTwoIdenticalRequestsExecuteConcurrently_ShouldExecuteHandlerOnlyOnce() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions()));
        services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddOptions<WebhookInboundOptions>();

        ServiceProvider sp = services.BuildServiceProvider();

        const string body = "{\"OrderId\":\"ORD-RACE-CONCURRENCY\"}";
        const string secret = "whsec_race_test_secret_123456789";
        HmacSha256WebhookSigner signer = new();
        WebhookSignature sig = signer.Sign(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(secret), UnixTimestamp.Now);

        int handlerExecutionCount = 0;
        Delegate slowHandler = async () => {
            Interlocked.Increment(ref handlerExecutionCount);
            await Task.Delay(100); // 100ms süren veritabanı işlemi
            return Results.Ok();
        };

        WebhookReceiverEndpointMetadata metadata = new() {
            SecretResolver = new SecretWebhookSecretResolver(Secret.From(secret)),
            EnforceIdempotency = true
        };
        WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, slowHandler);

        // Act: Aynı anda 2 istek başlatılır
        Task task1 = SendRequestAsync();
        Task task2 = SendRequestAsync();

        await Task.WhenAll(task1, task2);

        // Assert: Handler SADECE 1 KEZ çalışmalıdır!
        // ❌ MEVCUT KODDA PATLAR: handlerExecutionCount == 2 olur!
        Assert.Equal(1, handlerExecutionCount);

        async Task SendRequestAsync() {
            DefaultHttpContext ctx = new() { RequestServices = sp };
            ctx.Request.Path = "/api/webhooks/orders";
            ctx.Request.Method = "POST";
            ctx.Request.Headers[WebhookHeaderNames.WebhookSignature] = sig.HeaderValue;
            ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            ctx.Request.ContentLength = body.Length;

            EndpointFilterInvocationContext invCtx = new DefaultEndpointFilterInvocationContext(ctx);
            await filter.InvokeAsync(invCtx, static c => ValueTask.FromResult<object?>(Results.Ok()));
        }
    }
}