using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Wiaoj.Net;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

/// <summary>
/// The webhook transport enforces Wiaoj.Net's outbound network policy: a refused destination is a permanent delivery
/// failure, the registered client carries the policy, and a refused target URL can be reported as a result (#122).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "OutboundNetworkPolicy")]
public sealed class OutboundNetworkPolicyIntegrationTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>An HTTP/1.1 server on 127.0.0.1 answering 200, counting accepted connections.</summary>
    private sealed class LoopbackServer : IDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private int _connections;

        public LoopbackServer() {
            this._listener.Start();
            _ = Task.Run(this.AcceptAsync);
        }

        public Uri Url => new($"http://127.0.0.1:{((IPEndPoint)this._listener.LocalEndpoint).Port}/hook");

        public int Connections => Volatile.Read(ref this._connections);

        private async Task AcceptAsync() {
            try {
                while(!this._stop.IsCancellationRequested) {
                    using TcpClient client = await this._listener.AcceptTcpClientAsync(this._stop.Token);
                    Interlocked.Increment(ref this._connections);
                    NetworkStream stream = client.GetStream();
                    await stream.ReadAsync(new byte[8192], this._stop.Token);
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

    public sealed class Delivery {
        [Fact]
        public async Task Should_Report_A_Refused_Destination_As_A_Permanent_Invalid_Destination_Failure() {
            // Retrying a refused destination can never succeed; it must not spend the retry budget.
            using LoopbackServer server = new();
            HttpWebhookDeliverer deliverer = WebhookTestFactory.CreateDeliverer(
                new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));

            WebhookDeliveryResult result = await deliverer.DeliverAsync(
                WebhookTestFactory.CreateContext(endpoint: WebhookTestFactory.CreateEndpoint(server.Url)), Ct);

            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.InvalidDestination, failure.Reason);
            Assert.Equal(0, server.Connections);
        }
    }

    public sealed class Registration {
        private static async Task<HttpResponseMessage> SendThroughRegisteredClientAsync(Action<IWebhookBuilder>? configure, Uri url) {
            ServiceCollection services = new();
            services.AddLogging();
            IWebhookBuilder builder = services.AddWiaojWebhooks();
            configure?.Invoke(builder);

            await using ServiceProvider provider = services.BuildServiceProvider();
            HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(typeof(HttpWebhookSender).Name);
            using HttpClient client = new(handler, disposeHandler: false);
            return await client.GetAsync(url, Ct);
        }

        [Fact]
        public async Task Should_Refuse_Loopback_By_Default() {
            using LoopbackServer server = new();

            HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => SendThroughRegisteredClientAsync(null, server.Url));

            Assert.IsType<OutboundNetworkPolicyException>(error.InnerException);
            Assert.Equal(0, server.Connections);
        }

        [Fact]
        public async Task Should_Allow_A_Network_Named_In_The_Policy() {
            using LoopbackServer server = new();

            using HttpResponseMessage response = await SendThroughRegisteredClientAsync(
                b => b.UseNetworkPolicy(OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8")] }),
                server.Url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Should_Allow_Every_Destination_When_Private_Networks_Are_Allowed() {
            using LoopbackServer server = new();

            using HttpResponseMessage response = await SendThroughRegisteredClientAsync(b => b.AllowPrivateNetworks(), server.Url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public sealed class EndpointBuilder {
        private readonly FakeSecretProtector<WebhookSigningContext> _protector = new();

        private WebhookEndpointBuilder Builder(string url) {
            return new WebhookEndpointBuilder()
                .WithId("ep_policy")
                .WithTargetUrl(url)
                .WithSecret("whsec_policy_key_1234567890", this._protector);
        }

        [Theory]
        [InlineData("http://127.0.0.1/hook")]
        [InlineData("http://[::1]/hook")]
        [InlineData("http://169.254.169.254/latest/meta-data")]
        [InlineData("http://localhost/hook")]
        public async Task Should_Return_A_Refused_Target_As_A_Result(string url) {
            WebhookEndpointBuildResult result = await this.Builder(url).TryBuildAsync(Ct);

            Assert.False(result.IsSuccess);
            Assert.Equal(WebhookEndpointBuildStatus.DestinationRefused, result.Status);
            Assert.Null(result.Endpoint);
            Assert.Contains(new Uri(url).Host, result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Still_Throw_From_BuildAsync_For_A_Refused_Target() {
            await Assert.ThrowsAsync<WebhookSsrfBlockedException>(() => this.Builder("http://127.0.0.1/hook").BuildAsync(Ct));
        }

        [Fact]
        public async Task Should_Build_A_Target_In_A_Network_The_Policy_Allows() {
            WebhookEndpointBuildResult result = await this.Builder("http://10.20.5.1/hook")
                .WithNetworkPolicy(OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")] })
                .TryBuildAsync(Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal(new Uri("http://10.20.5.1/hook"), result.Endpoint.TargetUrl);
        }

        [Fact]
        public async Task Should_Let_Allowing_Private_Networks_Override_The_Policy() {
            WebhookEndpointBuildResult result = await this.Builder("http://127.0.0.1/hook")
                .WithSsrfValidation(validate: true, allowPrivateNetworks: true)
                .TryBuildAsync(Ct);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Should_Build_A_Public_Literal_Target() {
            Assert.True((await this.Builder("https://8.8.8.8/hook").TryBuildAsync(Ct)).IsSuccess);
        }

        [Fact]
        public async Task Should_Return_An_Unresolvable_Host_As_A_Result_And_Throw_The_Resolver_Error_From_BuildAsync() {
            // .invalid is reserved never to resolve (RFC 6761).
            WebhookEndpointBuildResult result = await this.Builder("https://webhooks.nonexistent.invalid/hook").TryBuildAsync(Ct);

            Assert.Equal(WebhookEndpointBuildStatus.DestinationUnresolvable, result.Status);
            Assert.IsType<SocketException>(result.ResolutionError);
            await Assert.ThrowsAsync<SocketException>(() => this.Builder("https://webhooks.nonexistent.invalid/hook").BuildAsync(Ct));
        }
    }
}
