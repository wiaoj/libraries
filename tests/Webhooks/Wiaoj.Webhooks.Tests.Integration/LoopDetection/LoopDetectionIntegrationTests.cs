using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.LoopDetection;
using Wiaoj.Webhooks.Transports.InMemory;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Integration.LoopDetection;

[Trait("Category", "Integration")]
[Trait("Feature", "LoopDetection")]
public sealed class LoopDetectionIntegrationTests : IAsyncLifetime {
    private WebApplication? _app;
    private readonly InMemoryTestEndpointResolver _endpointResolver = new();
    private readonly FakeSecretProtector<WebhookSigningContext> _secretProtector = new();
    private readonly ConcurrentQueue<(string EndpointId, int HopCount, string? CausalChain)> _receivedWebhooks = new();

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
                    .UseLoopDetection(options => {
                        options.MaxHops = 3;
                        options.InstanceId = "node-integration-test";
                        options.TrackCausalChain = true;
                        options.Behavior = LoopDetectedBehavior.DropAndLog;
                    })
                    .UseHmacSha256Signing();
        });

        builder.Services.AddHttpClient<HttpWebhookSender>()
            .ConfigurePrimaryHttpMessageHandler(sp => {
                TestServer server = (TestServer)sp.GetRequiredService<IServer>();
                return server.CreateHandler();
            });

        this._app = builder.Build();

        this._app.MapPost("/api/loop-receiver/{endpointId}", (string endpointId, HttpRequest request) => {
            int hop = 0;
            if(request.Headers.TryGetValue(WebhookHeaderNames.WebhookHopCount, out StringValues hopVal)) {
                int.TryParse(hopVal.ToString(), out hop);
            }

            request.Headers.TryGetValue(WebhookHeaderNames.WebhookCausalChain, out StringValues chainVal);

            this._receivedWebhooks.Enqueue((endpointId, hop, chainVal.ToString()));
            return Results.Ok();
        });

        await this._app.StartAsync(TestContext.Current.CancellationToken);

        this._dispatcher = this._app.Services.GetRequiredService<IWebhookDispatcher>();
        this._store = this._app.Services.GetRequiredService<IWebhookStore>();
    }

    public async ValueTask DisposeAsync() {
        if(this._app is not null) {
            await this._app.StopAsync(TestContext.Current.CancellationToken);
            await this._app.DisposeAsync();
        }
    }

    [Fact]
    public async Task DispatchAsync_SetsHopAndCausalHeaders_OnOutboundDelivery() {
        // Arrange
        WebhookEndpointId endpointId = new("ep_loop_01");
        Uri targetUri = new("http://localhost/api/loop-receiver/ep_loop_01");
        EncryptedSecret<WebhookSigningContext> secret = this._secretProtector.Protect("whsec_loop_test_key");

        WebhookEndpoint endpoint = new(endpointId, targetUri, secret);
        this._endpointResolver.Register(endpoint);

        LoopTestEvent @event = new("EVT-100");

        // Act
        WebhookDeliveryHandle handle = await this._dispatcher.DispatchAsync(endpointId, @event, cancellationToken: TestContext.Current.CancellationToken);

        // Assert - wait for delivery to receiver
        await Task.Delay(250, TestContext.Current.CancellationToken);

        Assert.True(this._receivedWebhooks.TryDequeue(out var received));
        Assert.Equal("ep_loop_01", received.EndpointId);
        Assert.Equal(1, received.HopCount);
        Assert.Equal("node-integration-test", received.CausalChain);

        WebhookJobRecord? record = await this._store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
        Assert.NotNull(record);
        Assert.Equal(WebhookJobStatus.Delivered, record.Status);
    }

    public sealed record LoopTestEvent(string Id) : IWebhookEvent;

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
