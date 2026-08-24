using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Feature", "InboundHub")]
public sealed class InboundHubIntegrationTests : IAsyncLifetime {
    private WebApplication? _app;
    private HttpClient _client = null!;
    private const string SecretKey = "ghsec_hub_integration_test_secret_123";
    private readonly GitHubWebhookSigner _signer = new();

    public sealed record PingDto(string Zen);
    public sealed record PushDto(string Ref, string Pusher);

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(new FakeSecretProtector<WebhookSigningContext>());

        builder.Services.AddWiaojWebhooks(webhooks => {
            webhooks.UseInMemoryTransport()
                    .AllowPrivateNetworks();

            webhooks.AddInbound(inbound => {
                inbound.AddPolicy("GitHub", policy => policy
                    .WithSigner<GitHubWebhookSigner>()
                    .WithEventFromHeader("X-GitHub-Event")
                    .UseSecret(Secret.From(SecretKey)));
            });
        });

        this._app = builder.Build();

        this._app.MapWebhook("/api/webhooks/github")
            .UsePolicy("GitHub")
            .OnPing()
            .On<PushDto>("push", static (PushDto push) => Results.Ok(new { handled = $"push:{push.Pusher}" }))
            .IgnoreUnhandledEvents();

        await this._app.StartAsync();
        this._client = this._app.GetTestClient();
    }

    public async ValueTask DisposeAsync() {
        if(this._app is not null) {
            await this._app.StopAsync();
            await this._app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Post_PingEvent_Returns200Ok_FromOnPing() {
        const string payload = """{"zen":"Responsive is better than fast."}""";
        WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(payload), Encoding.UTF8.GetBytes(SecretKey), UnixTimestamp.Now);

        HttpRequestMessage request = new(HttpMethod.Post, "/api/webhooks/github") {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "ping");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={sig.Signature}");

        HttpResponseMessage response = await this._client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_PushEvent_ExecutesMatchingDelegateSuccessfully() {
        const string payload = """{"Ref":"refs/heads/main","Pusher":"bertan"}""";
        WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(payload), Encoding.UTF8.GetBytes(SecretKey), UnixTimestamp.Now);

        HttpRequestMessage request = new(HttpMethod.Post, "/api/webhooks/github") {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "push");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={sig.Signature}");

        HttpResponseMessage response = await this._client.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("push:bertan", responseBody);
    }

    [Fact]
    public async Task Post_UnhandledEvent_Returns200Ok_BecauseIgnoreUnhandledIsTrue() {
        const string payload = """{"action":"starred"}""";
        WebhookSignature sig = this._signer.Sign(Encoding.UTF8.GetBytes(payload), Encoding.UTF8.GetBytes(SecretKey), UnixTimestamp.Now);

        HttpRequestMessage request = new(HttpMethod.Post, "/api/webhooks/github") {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "watch"); // Unhandled event
        request.Headers.Add("X-Hub-Signature-256", $"sha256={sig.Signature}");

        HttpResponseMessage response = await this._client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_TamperedSignature_Returns401Unauthorized() {
        const string payload = """{"Ref":"refs/heads/main","Pusher":"bertan"}""";

        HttpRequestMessage request = new(HttpMethod.Post, "/api/webhooks/github") {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "push");
        request.Headers.Add("X-Hub-Signature-256", "sha256=invalid_hash_0000000000000000000000000000000000000000000000000000000000000000");

        HttpResponseMessage response = await this._client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}