using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>
/// The policy refuses the connection itself — against a real loopback server, through a real SocketsHttpHandler — and
/// survives handler configuration registered before or after it (#121).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "OutboundNetworkPolicyHandler")]
public sealed class OutboundNetworkPolicyHandlerTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly OutboundNetworkPolicy LoopbackAllowed = OutboundNetworkPolicy.PublicOnly with {
        AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8"), IPNetwork.Parse("::1/128")]
    };

    /// <summary>A minimal HTTP/1.1 server on 127.0.0.1 that answers 200 and counts the connections it accepts.</summary>
    private sealed class LoopbackServer : IDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private int _connections;

        public LoopbackServer() {
            this._listener.Start();
            _ = Task.Run(this.AcceptAsync);
        }

        public int Port => ((IPEndPoint)this._listener.LocalEndpoint).Port;

        public int Connections => Volatile.Read(ref this._connections);

        private async Task AcceptAsync() {
            try {
                while(!this._stop.IsCancellationRequested) {
                    using TcpClient client = await this._listener.AcceptTcpClientAsync(this._stop.Token);
                    Interlocked.Increment(ref this._connections);
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[4096];
                    await stream.ReadAsync(buffer, this._stop.Token);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"), this._stop.Token);
                }
            }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { }
        }

        public void Dispose() {
            this._stop.Cancel();
            this._listener.Stop();
        }
    }

    private static async Task<HttpRequestException> RefusedAsync(HttpClient client, string url) {
        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url, Ct));
        Assert.IsType<OutboundNetworkPolicyException>(error.InnerException);
        return error;
    }

    public sealed class OnAHandler {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("localhost")]
        public async Task Should_Refuse_Loopback_Without_Connecting(string host) {
            using LoopbackServer server = new();
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));

            HttpRequestException error = await RefusedAsync(client, $"http://{host}:{server.Port}/");

            Assert.Equal(0, server.Connections);
            if(host == "localhost") {
                // The host is named; what an internal name resolves to is not.
                Assert.DoesNotContain("127.0.0.1", error.InnerException!.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("::1", error.InnerException!.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task Should_Connect_To_An_Allowed_Network() {
            using LoopbackServer server = new();
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(LoopbackAllowed));

            using HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{server.Port}/", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Should_Turn_Off_The_Environment_Proxy() {
            SocketsHttpHandler handler = new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly);

            Assert.False(handler.UseProxy);
            Assert.NotNull(handler.ConnectCallback);
        }

        [Fact]
        public void Should_Refuse_An_Explicit_Proxy() {
            SocketsHttpHandler handler = new() { Proxy = new WebProxy("http://proxy.example.com:3128") };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => handler.UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));
            Assert.Contains("proxy", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Refuse_To_Replace_An_Existing_Connect_Callback() {
            SocketsHttpHandler handler = new() { ConnectCallback = (_, _) => ValueTask.FromResult<Stream>(Stream.Null) };

            Assert.Throws<InvalidOperationException>(() => handler.UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));
        }
    }

    public sealed class OnARegisteredClient {
        private static HttpClient Client(Action<IServiceCollection> register, string name = "partner") {
            ServiceCollection services = new();
            register(services);
            return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient(name);
        }

        private static SocketsHttpHandler PrimaryHandler(Action<IServiceCollection> register, string name = "partner") {
            ServiceCollection services = new();
            register(services);
            HttpMessageHandler handler = services.BuildServiceProvider().GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);

            while(handler is DelegatingHandler delegating) {
                handler = delegating.InnerHandler!;
            }

            return Assert.IsType<SocketsHttpHandler>(handler);
        }

        [Fact]
        public async Task Should_Refuse_Loopback() {
            using LoopbackServer server = new();
            using HttpClient client = Client(s => s.AddHttpClient("partner").AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));

            await RefusedAsync(client, $"http://127.0.0.1:{server.Port}/");
            Assert.Equal(0, server.Connections);
        }

        [Fact]
        public async Task Should_Still_Protect_A_Primary_Handler_Configured_After_The_Policy() {
            // Registered later, this replaces the primary handler; the policy must not be lost with the old one.
            using LoopbackServer server = new();
            Action<IServiceCollection> register = s => s.AddHttpClient("partner")
                .AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

            using HttpClient client = Client(register);
            await RefusedAsync(client, $"http://127.0.0.1:{server.Port}/");

            SocketsHttpHandler primary = PrimaryHandler(register);
            Assert.NotNull(primary.ConnectCallback);
            Assert.False(primary.AllowAutoRedirect);
        }

        [Fact]
        public async Task Should_Keep_The_Settings_Of_A_Primary_Handler_Configured_Before_The_Policy() {
            using LoopbackServer server = new();
            Action<IServiceCollection> register = s => s.AddHttpClient("partner")
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
                .AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly);

            using HttpClient client = Client(register);
            await RefusedAsync(client, $"http://127.0.0.1:{server.Port}/");
            Assert.False(PrimaryHandler(register).AllowAutoRedirect);
        }

        [Fact]
        public async Task Should_Apply_A_Policy_Derived_From_Public_Only() {
            using LoopbackServer server = new();
            using HttpClient client = Client(s => s.AddHttpClient("partner")
                .AddOutboundNetworkPolicy(policy => policy with { AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8")] }));

            using HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{server.Port}/", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Should_Leave_Other_Clients_Alone() {
            using LoopbackServer server = new();
            using HttpClient client = Client(s => {
                s.AddHttpClient("partner").AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly);
                s.AddHttpClient("internal");
            }, name: "internal");

            using HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{server.Port}/", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Should_Refuse_To_Create_A_Client_Whose_Primary_Handler_Cannot_Enforce_It() {
            // Connecting unprotected would be worse than failing.
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Client(s => s.AddHttpClient("partner")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
                .AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly)));

            Assert.Contains("SocketsHttpHandler", error.Message, StringComparison.Ordinal);
        }
    }
}
