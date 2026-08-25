using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Text;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Transports.InMemory;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Integration.Idempotency;

[Trait("Category", "Integration")]
[Trait("Feature", "Idempotency")]
public sealed class IdempotencyIntegrationTests : IAsyncLifetime {
    private WebApplication? _app;
    private readonly InMemoryTestEndpointResolver _endpointResolver = new();
    private readonly FakeSecretProtector<WebhookSigningContext> _secretProtector = new();
    private readonly ConcurrentDictionary<string, string> _secretRegistry = new();
    private readonly ConcurrentQueue<(string EndpointId, string Payload)> _receivedWebhooks = new();

    private IWebhookDispatcher _dispatcher = null!;
    private IWebhookStore _store = null!;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(this._secretProtector);
        builder.Services.AddSingleton<IWebhookEndpointResolver>(this._endpointResolver);

        builder.Services.AddWiaojWebhooks(webhooks => {
            webhooks.UseInMemoryTransport()
                    .AllowPrivateNetworks()
                    .UseIdempotency(TimeSpan.FromSeconds(2))
                    .UseHmacSha256Signing();
        });

        builder.Services.AddHttpClient<HttpWebhookSender>()
            .ConfigurePrimaryHttpMessageHandler(sp => {
                TestServer server = (TestServer)sp.GetRequiredService<IServer>();
                return server.CreateHandler();
            });

        this._app = builder.Build();

        this._app.MapPost("/api/idempotency-receiver/{endpointId}", async (
            string endpointId,
            HttpRequest request,
            IWebhookSigner signer) => {
                if(!request.Headers.TryGetValue(signer.HeaderName, out StringValues signature)) {
                    return Results.Unauthorized();
                }

                if(!this._secretRegistry.TryGetValue(endpointId, out string? rawSecret)) {
                    return Results.BadRequest();
                }

                using StreamReader reader = new(request.Body, Encoding.UTF8);
                string payload = await reader.ReadToEndAsync();

                bool isValid = signer.Verify(
                    Encoding.UTF8.GetBytes(payload),
                    signature.ToString(),
                    Encoding.UTF8.GetBytes(rawSecret),
                    TimeSpan.FromMinutes(5));

                if(!isValid) {
                    return Results.Unauthorized();
                }

                this._receivedWebhooks.Enqueue((endpointId, payload));
                return Results.Ok(new { Status = "Processed" });
            });

        await this._app.StartAsync();

        this._dispatcher = this._app.Services.GetRequiredService<IWebhookDispatcher>();
        this._store = this._app.Services.GetRequiredService<IWebhookStore>();
    }

    public async ValueTask DisposeAsync() {
        if(this._app is not null) {
            await this._app.StopAsync();
            await this._app.DisposeAsync();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST SCENARIOS
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_WhenDuplicateEventWithinWindow_SuppressesSecondDelivery() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpoint, string _) = CreateEndpoint("http://localhost/api/idempotency-receiver");

        TestInvoiceEvent invoice = new("INV-100", 150.00m);

        WebhookDeliveryHandle handle1 = await this._dispatcher.DispatchAsync(endpoint.Id, invoice, ct);
        WebhookJobRecord? job1 = await WaitForJobStatusAsync(this._store, handle1.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);
        Assert.NotNull(job1);

        WebhookDeliveryHandle handle2 = await this._dispatcher.DispatchAsync(endpoint.Id, invoice, ct);
        WebhookJobRecord? job2 = await WaitForJobStatusAsync(this._store, handle2.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(job2);
        WebhookDeliveryAttempt item = Assert.Single(job2.Attempts);
        Assert.IsType<WebhookDeliveryResult.Deduplicated>(item.Result);
        Assert.Single(this._receivedWebhooks);
    }

    [Fact]
    public async Task DispatchAsync_WhenWindowExpires_AllowsSubsequentDelivery() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpoint, string _) = CreateEndpoint("http://localhost/api/idempotency-receiver");

        TestInvoiceEvent invoice = new("INV-200", 300.00m);

        // 1. Initial delivery
        WebhookDeliveryHandle handle1 = await this._dispatcher.DispatchAsync(endpoint.Id, invoice, ct);
        WebhookJobRecord? job1 = await WaitForJobStatusAsync(this._store, handle1.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(10), ct);

        Assert.NotNull(job1);
        Assert.Equal(WebhookJobStatus.Delivered, job1.Status);
        Assert.Single(this._receivedWebhooks);

        // 2. Wait for TTL expiration (Window is 2 seconds, waiting 2.5s)
        await Task.Delay(TimeSpan.FromSeconds(2.5), ct);

        // 3. Subsequent delivery after window expiry -> Must deliver again!
        WebhookDeliveryHandle handle2 = await this._dispatcher.DispatchAsync(endpoint.Id, invoice, ct);
        WebhookJobRecord? job2 = await WaitForJobStatusAsync(this._store, handle2.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(10), ct);

        Assert.NotNull(job2);
        Assert.Equal(WebhookJobStatus.Delivered, job2.Status);
        WebhookDeliveryAttempt item = Assert.Single(job2.Attempts);
        Assert.True(item.IsSuccess);
        Assert.IsType<WebhookDeliveryResult.Delivered>(job2.Attempts[0].Result);
        Assert.Equal(2, this._receivedWebhooks.Count);
    }

    [Fact]
    public async Task DispatchAsync_SameEventToDifferentEndpoints_DeliversToBoth() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpointA, string _) = CreateEndpoint("http://localhost/api/idempotency-receiver");
        (WebhookEndpoint endpointB, string _) = CreateEndpoint("http://localhost/api/idempotency-receiver");

        TestInvoiceEvent invoice = new("INV-SHARED", 999.99m);

        WebhookDeliveryHandle handleA = await this._dispatcher.DispatchAsync(endpointA.Id, invoice, ct);
        WebhookDeliveryHandle handleB = await this._dispatcher.DispatchAsync(endpointB.Id, invoice, ct);

        WebhookJobRecord? jobA = await WaitForJobStatusAsync(this._store, handleA.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);
        WebhookJobRecord? jobB = await WaitForJobStatusAsync(this._store, handleB.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(jobA);
        Assert.NotNull(jobB);
        Assert.Equal(2, this._receivedWebhooks.Count);
        Assert.Contains(this._receivedWebhooks, w => w.EndpointId == endpointA.Id.Value);
        Assert.Contains(this._receivedWebhooks, w => w.EndpointId == endpointB.Id.Value);
    }

    [Fact]
    public async Task DispatchAsync_WhenPayloadPropertiesDiffer_DeliversBothAsDistinctEvents() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpoint, string _) = CreateEndpoint("http://localhost/api/idempotency-receiver");

        TestInvoiceEvent invoice1 = new("INV-101", 50.00m);
        TestInvoiceEvent invoice2 = new("INV-102", 50.00m);

        WebhookDeliveryHandle handle1 = await this._dispatcher.DispatchAsync(endpoint.Id, invoice1, ct);
        WebhookDeliveryHandle handle2 = await this._dispatcher.DispatchAsync(endpoint.Id, invoice2, ct);

        WebhookJobRecord? job1 = await WaitForJobStatusAsync(this._store, handle1.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);
        WebhookJobRecord? job2 = await WaitForJobStatusAsync(this._store, handle2.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(job1);
        Assert.NotNull(job2);
        Assert.Equal(2, this._receivedWebhooks.Count);
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST HELPERS & DATA
    // ────────────────────────────────────────────────────────────────────────

    private (WebhookEndpoint Endpoint, string RawSecret) CreateEndpoint(string baseUrl) {
        string rawSecret = "whsec_test_idempotency_secret_32bytes_long";
        EncryptedSecret<WebhookSigningContext> encryptedSecret = this._secretProtector.Protect(rawSecret);

        WebhookEndpointId endpointId = new($"ep_idemp_{Guid.NewGuid():N}");
        Uri targetUrl = new($"{baseUrl.TrimEnd('/')}/{endpointId.Value}");

        WebhookEndpoint endpoint = new(endpointId, targetUrl, encryptedSecret);
        this._endpointResolver.Register(endpoint);
        this._secretRegistry[endpoint.Id.Value] = rawSecret;

        return (endpoint, rawSecret);
    }

    private static async Task<WebhookJobRecord?> WaitForJobStatusAsync(
        IWebhookStore store,
        WebhookJobId jobId,
        WebhookJobStatus targetStatus,
        TimeSpan timeout,
        CancellationToken testCancellation) {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(testCancellation);
        cts.CancelAfter(timeout);

        try {
            while(!cts.IsCancellationRequested) {
                WebhookJobRecord? job = await store.GetJobAsync(jobId, testCancellation);
                if(job is not null && job.Status == targetStatus) {
                    return job;
                }
                await Task.Delay(25, cts.Token);
            }
        }
        catch(OperationCanceledException) when(!testCancellation.IsCancellationRequested) { }

        return await store.GetJobAsync(jobId, testCancellation);
    }

    // Public accessibility for clean serialization
    public sealed record TestInvoiceEvent(string InvoiceId, decimal Amount) : IWebhookEvent {
        public static string EventName => "test.invoice.created";
    }

    private sealed class InMemoryTestEndpointResolver : IWebhookEndpointResolver {
        private readonly ConcurrentDictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new();

        public void Register(WebhookEndpoint endpoint) {
            this._endpoints[endpoint.Id] = endpoint;
        }

        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
        }
    }
}