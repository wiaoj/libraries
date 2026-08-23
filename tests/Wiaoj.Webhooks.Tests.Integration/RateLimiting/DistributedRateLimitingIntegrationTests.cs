using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Text;
using Wiaoj.DistributedCounter;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Transports.InMemory;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Integration.RateLimiting;

[Trait("Category", "Integration")]
[Trait("Feature", "RateLimiting")]
public sealed class DistributedRateLimitingIntegrationTests : IAsyncLifetime {
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

        builder.Services.AddDistributedCounter(dc => dc.UseInMemory());

        builder.Services.AddWiaojWebhooks(webhooks => {
            webhooks.UseInMemoryTransport()
                    .AllowPrivateNetworks()
                    .UseExponentialBackoffRetry(new ExponentialBackoffOptions {
                        MaxAttempts = 5,
                        InitialDelay = TimeSpan.FromMilliseconds(100),
                        Multiplier = 1.5,
                        Jitter = null
                    })
                    .UseDistributedRateLimiting(maxRequestsPerWindow: 2, window: TimeSpan.FromSeconds(1.5))
                    .UseHmacSha256Signing();
        });

        builder.Services.AddHttpClient<HttpWebhookSender>()
            .ConfigurePrimaryHttpMessageHandler(sp => {
                TestServer server = (TestServer)sp.GetRequiredService<IServer>();
                return server.CreateHandler();
            });

        this._app = builder.Build();

        this._app.MapPost("/api/ratelimit-receiver/{endpointId}", async (
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
                return Results.Ok(new { Status = "Accepted" });
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
    public async Task DispatchAsync_WhenUnderRateLimit_DeliversAllImmediately() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpoint, string _) = CreateEndpoint("http://localhost/api/ratelimit-receiver");

        RateLimitTestEvent event1 = new("PAY-101", 10.0m);
        RateLimitTestEvent event2 = new("PAY-102", 20.0m);

        WebhookDeliveryHandle handle1 = await this._dispatcher.DispatchAsync(endpoint.Id, event1, ct);
        WebhookDeliveryHandle handle2 = await this._dispatcher.DispatchAsync(endpoint.Id, event2, ct);

        WebhookJobRecord? job1 = await WaitForJobStatusAsync(this._store, handle1.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);
        WebhookJobRecord? job2 = await WaitForJobStatusAsync(this._store, handle2.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(job1);
        Assert.NotNull(job2);
        Assert.Single(job1.Attempts);
        Assert.Single(job2.Attempts);
        Assert.True(job1.Attempts[0].IsSuccess);
        Assert.True(job2.Attempts[0].IsSuccess);
        Assert.Equal(2, this._receivedWebhooks.Count);
    }

    [Fact]
    public async Task DispatchAsync_WhenRateLimitExceeded_ThrottlesAndRecoversAfterWindowElapses() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpoint, string _) = CreateEndpoint("http://localhost/api/ratelimit-receiver");

        RateLimitTestEvent event1 = new("PAY-201", 10.0m);
        RateLimitTestEvent event2 = new("PAY-202", 20.0m);
        RateLimitTestEvent event3 = new("PAY-203", 30.0m);

        WebhookDeliveryHandle handle1 = await this._dispatcher.DispatchAsync(endpoint.Id, event1, ct);
        WebhookDeliveryHandle handle2 = await this._dispatcher.DispatchAsync(endpoint.Id, event2, ct);
        WebhookDeliveryHandle handle3 = await this._dispatcher.DispatchAsync(endpoint.Id, event3, ct);

        WebhookJobRecord? job1 = await WaitForJobStatusAsync(this._store, handle1.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);
        WebhookJobRecord? job2 = await WaitForJobStatusAsync(this._store, handle2.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(job1);
        Assert.NotNull(job2);

        WebhookJobRecord? job3 = await WaitForJobStatusAsync(this._store, handle3.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(8), ct);

        Assert.NotNull(job3);
        Assert.True(job3.Attempts.Count >= 2, $"Expected at least 2 attempts due to rate limit re-enqueue, found {job3.Attempts.Count}");

        Assert.False(job3.Attempts[0].IsSuccess);
        Assert.IsType<WebhookDeliveryResult.TransientFailure>(job3.Attempts[0].Result);
        WebhookDeliveryResult.TransientFailure transient = (WebhookDeliveryResult.TransientFailure)job3.Attempts[0].Result;
        Assert.Equal(429, transient.StatusCode);

        Assert.True(job3.Attempts[^1].IsSuccess);
        Assert.Equal(3, this._receivedWebhooks.Count);
    }

    [Fact]
    public async Task DispatchAsync_ThrottlingOnEndpointA_DoesNotThrottleEndpointB() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpointA, string _) = CreateEndpoint("http://localhost/api/ratelimit-receiver");
        (WebhookEndpoint endpointB, string _) = CreateEndpoint("http://localhost/api/ratelimit-receiver");

        await this._dispatcher.DispatchAsync(endpointA.Id, new RateLimitTestEvent("A-1", 10.0m), ct);
        await this._dispatcher.DispatchAsync(endpointA.Id, new RateLimitTestEvent("A-2", 20.0m), ct);

        WebhookDeliveryHandle handleB = await this._dispatcher.DispatchAsync(endpointB.Id, new RateLimitTestEvent("B-1", 50.0m), ct);
        WebhookJobRecord? jobB = await WaitForJobStatusAsync(this._store, handleB.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(jobB);
        Assert.Single(jobB.Attempts);
        Assert.True(jobB.Attempts[0].IsSuccess);
    }

    // ────────────────────────────────────────────────────────────────────────
    // TEST DATA & HELPERS
    // ────────────────────────────────────────────────────────────────────────

    private (WebhookEndpoint Endpoint, string RawSecret) CreateEndpoint(string baseUrl) {
        string rawSecret = "whsec_test_ratelimit_secret_32bytes_long";
        EncryptedSecret<WebhookSigningContext> encryptedSecret = this._secretProtector.Protect(rawSecret);

        WebhookEndpointId endpointId = new($"ep_rl_{Guid.NewGuid():N}");
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
    public sealed record RateLimitTestEvent(string PaymentId, decimal Amount) : IWebhookEvent {
        public static string EventName => "test.payment.created";
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