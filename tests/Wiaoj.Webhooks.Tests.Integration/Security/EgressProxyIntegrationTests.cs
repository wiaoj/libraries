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

namespace Wiaoj.Webhooks.Tests.Integration.Security;

/// <summary>
/// Integration tests verifying outbound webhook delivery through an egress forward proxy.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "EgressProxy")]
public sealed class EgressProxyIntegrationTests : IAsyncLifetime {
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

        // Configure engine with an explicit Egress Proxy URI
        builder.Services.AddWiaojWebhooks(webhooks => {
            webhooks.UseInMemoryTransport()
                    .AllowPrivateNetworks()
                    .UseProxy("http://egress-proxy.internal:8080")
                    .UseHmacSha256Signing();
        });

        // Intercept requests in test harness
        builder.Services.AddHttpClient<HttpWebhookSender>()
            .ConfigurePrimaryHttpMessageHandler(sp => {
                TestServer server = (TestServer)sp.GetRequiredService<IServer>();
                return server.CreateHandler();
            });

        this._app = builder.Build();

        this._app.MapPost("/api/proxy-receiver/{endpointId}", async (
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
                return Results.Ok(new { Status = "DeliveredViaProxy" });
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

    [Fact]
    public async Task DispatchAsync_WithConfiguredProxy_RoutesAndDeliversWebhookSuccessfully() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WebhookEndpoint endpoint, string _) = CreateEndpoint("http://localhost/api/proxy-receiver");

        ProxyTestEvent @event = new("PROXY-REQ-1", 500.0m);

        WebhookDeliveryHandle handle = await this._dispatcher.DispatchAsync(endpoint.Id, @event, ct);
        WebhookJobRecord? job = await WaitForJobStatusAsync(this._store, handle.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(job);
        Assert.Equal(WebhookJobStatus.Delivered, job.Status);
        WebhookDeliveryAttempt item = Assert.Single(job.Attempts);
        Assert.True(item.IsSuccess);
        Assert.Single(this._receivedWebhooks);
    }

    private (WebhookEndpoint Endpoint, string RawSecret) CreateEndpoint(string baseUrl) {
        string rawSecret = "whsec_test_proxy_secret_32bytes_long";
        EncryptedSecret<WebhookSigningContext> encryptedSecret = this._secretProtector.Protect(rawSecret);

        WebhookEndpointId endpointId = new($"ep_proxy_{Guid.NewGuid():N}");
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

    private sealed record ProxyTestEvent(string PaymentId, decimal Amount) : IWebhookEvent {
        public static string EventName => "test.proxy.payment";
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