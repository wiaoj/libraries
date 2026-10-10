using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.LoopDetection;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Integration.LoopDetection;

/// <summary>
/// Two services whose inbound handlers re-publish the event they receive to each other: A → B → A → B …
/// Loop detection must stop the ping-pong instead of letting it run forever.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "LoopDetection")]
public sealed class PingPongLoopIntegrationTests {
    // A safety net so a broken build does not loop forever: no handler re-publishes beyond this bounce.
    private const int SafetyCap = 20;

    private const int PortA = 5001;
    private const int PortB = 5002;

    private readonly FakeSecretProtector<WebhookSigningContext> _secretProtector = new();
    private readonly ConcurrentDictionary<int, HttpMessageHandler> _servers = new();
    private readonly ConcurrentQueue<Received> _received = new();

    [Fact]
    public async Task PingPong_StopsAtTheFirstRepeatedService() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using PingPongHost a = await this.StartAsync("service-a", PortA, PortB, options => options.MaxHops = 10, ct);
        await using PingPongHost b = await this.StartAsync("service-b", PortB, PortA, options => options.MaxHops = 10, ct);

        await a.Dispatcher.DispatchAsync(a.Peer, new PingPongEvent(1), ct);
        await this.WaitUntilQuietAsync(ct);

        // A → B (hop 1, chain "service-a"), B → A (hop 2, chain "service-a, service-b"),
        // then A's re-publish is dropped: service-a is already in its causal chain.
        Received[] received = [.. this._received];
        Assert.Equal(2, received.Length);
        Assert.Equal(new Received("service-b", 1, 1, "service-a"), received[0]);
        Assert.Equal(new Received("service-a", 2, 2, "service-a, service-b"), received[1]);

        WebhookJobRecord dropped = await WaitForDeadLetterAsync(a.Store, ct);
        WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(Assert.Single(dropped.Attempts).Result);
        Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
    }

    [Fact]
    public async Task PingPong_WithoutCausalChain_StopsAtMaxHops() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using PingPongHost a = await this.StartAsync("service-a", PortA, PortB, options => {
            options.MaxHops = 4;
            options.TrackCausalChain = false;
        }, ct);
        await using PingPongHost b = await this.StartAsync("service-b", PortB, PortA, options => {
            options.MaxHops = 4;
            options.TrackCausalChain = false;
        }, ct);

        await a.Dispatcher.DispatchAsync(a.Peer, new PingPongEvent(1), ct);
        await this.WaitUntilQuietAsync(ct);

        // Hops 1..4 are delivered; the fifth send would exceed MaxHops and is dropped by A.
        Received[] received = [.. this._received];
        Assert.Equal([1, 2, 3, 4], received.Select(r => r.HopCount));
        Assert.Equal(["service-b", "service-a", "service-b", "service-a"], received.Select(r => r.Service));

        WebhookJobRecord dropped = await WaitForDeadLetterAsync(a.Store, ct);
        WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(Assert.Single(dropped.Attempts).Result);
        Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
    }

    private async Task<PingPongHost> StartAsync(
        string serviceName,
        int port,
        int peerPort,
        Action<LoopDetectionOptions> configureLoopDetection,
        CancellationToken ct) {

        WebhookEndpointId peer = new($"to-{peerPort}");
        PeerEndpointResolver resolver = new(new WebhookEndpoint(
            peer,
            new Uri($"http://localhost:{peerPort}/hooks"),
            this._secretProtector.Protect("whsec_ping_pong")));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(this._secretProtector);
        builder.Services.AddSingleton<IWebhookEndpointResolver>(resolver);

        builder.Services.AddWiaojWebhooks(webhooks => {
            webhooks.UseInMemoryTransport()
                    .AllowPrivateNetworks()
                    .RegisterEvent<PingPongEvent>()
                    .UseStandardHeaders()
                    .UseLoopDetection(options => {
                        options.ServiceName = serviceName;
                        configureLoopDetection(options);
                    });

            webhooks.AddInbound();
        });

        // Outbound deliveries are routed to the peer's in-process test server by port.
        builder.Services.AddHttpClient<HttpWebhookSender>()
            .ConfigurePrimaryHttpMessageHandler(() => new PortRoutingHandler(this._servers));

        WebApplication app = builder.Build();

        app.MapWebhook<PingPongEvent>("/hooks", async (PingPongEvent @event, HttpContext http, IWebhookDispatcher dispatcher, CancellationToken cancellationToken) => {
            int.TryParse(http.Request.Headers[WebhookHeaderNames.WebhookHopCount], out int hop);
            string chain = http.Request.Headers[WebhookHeaderNames.WebhookCausalChain].ToString();
            this._received.Enqueue(new Received(serviceName, @event.Bounce, hop, chain));

            if(@event.Bounce < SafetyCap) {
                await dispatcher.DispatchAsync(peer, new PingPongEvent(@event.Bounce + 1), cancellationToken);
            }
        })
            .AllowUnsigned()
            .DisableIdempotency();

        await app.StartAsync(ct);
        this._servers[port] = ((TestServer)app.Services.GetRequiredService<IServer>()).CreateHandler();

        return new PingPongHost(app, peer);
    }

    /// <summary>Waits until no new delivery has arrived for a while, or the safety cap is reached.</summary>
    private async Task WaitUntilQuietAsync(CancellationToken ct) {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        int lastCount = -1;
        DateTime lastChange = DateTime.UtcNow;
        while(!timeout.IsCancellationRequested) {
            int count = this._received.Count;
            if(count >= SafetyCap) {
                return;
            }

            if(count != lastCount) {
                lastCount = count;
                lastChange = DateTime.UtcNow;
            }
            else if(count > 0 && DateTime.UtcNow - lastChange > TimeSpan.FromMilliseconds(750)) {
                return;
            }

            try {
                await Task.Delay(25, timeout.Token);
            }
            catch(OperationCanceledException) when(!ct.IsCancellationRequested) {
                return;
            }
        }
    }

    private static async Task<WebhookJobRecord> WaitForDeadLetterAsync(IWebhookStore store, CancellationToken ct) {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        while(true) {
            WebhookJobRecord? deadLettered = (await store.GetDeadLetteredJobsAsync(10, timeout.Token)).FirstOrDefault();
            if(deadLettered is not null) {
                return deadLettered;
            }

            await Task.Delay(25, timeout.Token);
        }
    }

    [WebhookEvent("ping.pong")]
    public sealed record PingPongEvent(int Bounce) : IWebhookEvent;

    private sealed record Received(string Service, int Bounce, int HopCount, string CausalChain);

    private sealed class PingPongHost(WebApplication app, WebhookEndpointId peer) : IAsyncDisposable {
        public WebhookEndpointId Peer { get; } = peer;
        public IWebhookDispatcher Dispatcher => app.Services.GetRequiredService<IWebhookDispatcher>();
        public IWebhookStore Store => app.Services.GetRequiredService<IWebhookStore>();

        public async ValueTask DisposeAsync() {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class PortRoutingHandler(ConcurrentDictionary<int, HttpMessageHandler> servers) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpMessageInvoker invoker = new(servers[request.RequestUri!.Port], disposeHandler: false);
            return invoker.SendAsync(request, cancellationToken);
        }
    }

    private sealed class PeerEndpointResolver(WebhookEndpoint peer) : IWebhookEndpointResolver {
        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(endpointId == peer.Id ? peer : null);
        }
    }
}
