using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using Wiaoj.Net;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

/// <summary>
/// A proxy no longer turns SSRF protection off silently: it must be acknowledged at startup, and destinations are still
/// checked before they are sent to the proxy (#123).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "ProxySsrf")]
public sealed class ProxySsrfProtectionTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WebhookSecurityOptions ResolveOptions(Action<IWebhookBuilder> configure) {
        ServiceCollection services = new();
        services.AddLogging();
        configure(services.AddWiaojWebhooks());
        return services.BuildServiceProvider().GetRequiredService<IOptions<WebhookSecurityOptions>>().Value;
    }

    public sealed class StartupValidation {
        [Fact]
        public void Should_Fail_When_A_Proxy_Is_Set_With_Protection_On_And_No_Acknowledgement() {
            OptionsValidationException error = Assert.Throws<OptionsValidationException>(() =>
                ResolveOptions(b => b.UseProxy("http://egress.example.com:3128")));

            Assert.Contains("ProxyEnforcesEgressPolicy", error.Message, StringComparison.Ordinal);
            Assert.Contains("AllowPrivateNetworks", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Start_When_The_Proxy_Is_Acknowledged_To_Enforce_Egress() {
            WebhookSecurityOptions options = ResolveOptions(b => b
                .UseProxy("http://egress.example.com:3128")
                .ConfigureSecurity(o => o.ProxyEnforcesEgressPolicy = true));

            Assert.True(options.ProxyEnforcesEgressPolicy);
        }

        [Fact]
        public void Should_Start_When_Protection_Is_Explicitly_Off() {
            WebhookSecurityOptions options = ResolveOptions(b => b.UseProxy("http://egress.example.com:3128").AllowPrivateNetworks());

            Assert.True(options.AllowPrivateNetworks);
        }

        [Fact]
        public void Should_Start_Without_A_Proxy() {
            Assert.Null(ResolveOptions(_ => { }).Proxy);
        }

        [Fact]
        public void Should_Report_A_Missing_Network_Policy_As_A_Validation_Failure_Not_A_Crash() {
            // The validator used to catch only ArgumentOutOfRangeException, so this escaped as a raw ArgumentNullException.
            Assert.Throws<OptionsValidationException>(() => ResolveOptions(b => b.ConfigureSecurity(o => o.NetworkPolicy = null!)));
        }
    }

    public sealed class DestinationCheckThroughAProxy {
        /// <summary>Stands in for the proxy: records what would have been sent to it.</summary>
        private sealed class RecordingProxy : HttpMessageHandler {
            public List<Uri> Sent { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                lock(this.Sent) {
                    this.Sent.Add(request.RequestUri!);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
            }
        }

        private static async Task<(WebhookDeliveryResult Result, RecordingProxy Proxy)> DeliverAsync(string targetUrl, Action<IWebhookBuilder> configure) {
            RecordingProxy proxy = new();
            ServiceCollection services = new();
            services.AddLogging();
            configure(services.AddWiaojWebhooks());
            services.AddHttpClient<HttpWebhookSender>().ConfigurePrimaryHttpMessageHandler(() => proxy);

            await using ServiceProvider provider = services.BuildServiceProvider();
            IWebhookDeliverer deliverer = provider.GetRequiredService<IWebhookDeliverer>();

            WebhookDeliveryResult result = await deliverer.DeliverAsync(
                WebhookTestFactory.CreateContext(endpoint: WebhookTestFactory.CreateEndpoint(new Uri(targetUrl))), Ct);

            return (result, proxy);
        }

        private static void AcknowledgedProxy(IWebhookBuilder builder) {
            builder.UseProxy("http://egress.example.com:3128").ConfigureSecurity(o => o.ProxyEnforcesEgressPolicy = true);
        }

        [Theory]
        [InlineData("http://169.254.169.254/latest/meta-data")]
        [InlineData("http://127.0.0.1:6379/")]
        [InlineData("http://[::1]/hook")]
        [InlineData("http://10.0.0.5/hook")]
        public async Task Should_Refuse_An_IP_Literal_Before_It_Reaches_The_Proxy(string targetUrl) {
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync(targetUrl, AcknowledgedProxy);

            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.InvalidDestination, failure.Reason);
            Assert.Empty(proxy.Sent);
        }

        [Fact]
        public async Task Should_Refuse_A_Name_That_Resolves_Only_To_Refused_Addresses() {
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync("http://localhost/hook", AcknowledgedProxy);

            Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Empty(proxy.Sent);
        }

        [Fact]
        public async Task Should_Send_A_Public_Destination_To_The_Proxy() {
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync("https://8.8.8.8/hook", AcknowledgedProxy);

            Assert.True(result.IsSuccess);
            Assert.Single(proxy.Sent);
        }

        [Fact]
        public async Task Should_Leave_A_Name_It_Cannot_Resolve_To_The_Proxy() {
            // A proxy is often there because this host has no DNS for the outside world; refusing here would break delivery.
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync("https://webhooks.nonexistent.invalid/hook", AcknowledgedProxy);

            Assert.True(result.IsSuccess);
            Assert.Single(proxy.Sent);
        }

        [Fact]
        public async Task Should_Allow_A_Network_Named_In_The_Policy() {
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync("http://10.20.5.1/hook", b => {
                AcknowledgedProxy(b);
                b.UseNetworkPolicy(OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")] });
            });

            Assert.True(result.IsSuccess);
            Assert.Single(proxy.Sent);
        }

        [Fact]
        public async Task Should_Check_Nothing_When_Protection_Is_Explicitly_Off() {
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync("http://169.254.169.254/",
                b => b.UseProxy("http://egress.example.com:3128").AllowPrivateNetworks());

            Assert.True(result.IsSuccess);
            Assert.Single(proxy.Sent);
        }

        [Fact]
        public async Task Should_Leave_Direct_Connections_To_The_Connection_Time_Policy() {
            // Without a proxy the address actually connected to is checked when the socket opens (#122), so this check stays out.
            (WebhookDeliveryResult result, RecordingProxy proxy) = await DeliverAsync("http://169.254.169.254/", _ => { });

            Assert.True(result.IsSuccess);
            Assert.Single(proxy.Sent);
        }
    }
}
