using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Wiaoj.WellKnown.Discovery;

namespace Wiaoj.WellKnown.Tests.Integration;

/// <summary>
/// A protected resource, a Vaultex-style authorization server and a discovering client, each on its own https origin:
/// the client starts from a 401 and ends with the endpoints to obtain a token from (#115).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "WellKnown")]
[Trait("Component", "DiscoveryEndToEnd")]
public sealed class DiscoveryEndToEndTests {
    private const string Resource = "https://api.example.com/v1";
    private const string Vaultex = "https://vaultex.example.com";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Routes each request to the test server for its host, as DNS would, and counts requests per URL.</summary>
    private sealed class Network(IReadOnlyDictionary<string, HttpMessageHandler> hosts) : DelegatingHandler {
        public ConcurrentDictionary<string, int> Requests { get; } = new(StringComparer.Ordinal);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            this.Requests.AddOrUpdate(request.RequestUri!.AbsoluteUri, 1, (_, n) => n + 1);

            return hosts.TryGetValue(request.RequestUri.Authority, out HttpMessageHandler? host)
                ? new HttpMessageInvoker(host, disposeHandler: false).SendAsync(request, cancellationToken)
                : throw new HttpRequestException($"No host '{request.RequestUri.Authority}'.");
        }
    }

    private sealed class Deployment : IAsyncDisposable {
        public required WebApplication Api { get; init; }
        public required WebApplication AuthorizationServer { get; init; }
        public required Network Network { get; init; }

        public HttpClient Client() => new(this.Network, disposeHandler: false);

        public async ValueTask DisposeAsync() {
            await this.Api.DisposeAsync();
            await this.AuthorizationServer.DisposeAsync();
        }
    }

    private static async Task<Deployment> DeployAsync(params string[] authorizationServers) {
        WebApplication api = TestApp.Build(
            s => {
                s.AddOAuthProtectedResource(r => {
                    r.Resource = Resource;
                    r.AuthorizationServers.AddRange(authorizationServers.Length == 0 ? [Vaultex] : authorizationServers);
                    r.Scopes.Add("keys:read");
                    r.CacheDuration = TimeSpan.FromMinutes(5);
                });
                s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters {
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef0123456789abcdef")),
                        ValidIssuer = Vaultex,
                        ValidAudience = Resource
                    })
                    .AddProtectedResourceMetadataChallenge();
                s.AddAuthorization();
            },
            a => {
                a.UseAuthentication();
                a.UseAuthorization();
                a.MapOAuthProtectedResource();
                a.MapGet("/v1/keys", () => "secret").RequireAuthorization();
                a.MapGet("/v1", () => "root").RequireAuthorization();
            });

        WebApplication vaultex = TestApp.Build(
            s => s.AddOAuthAuthorizationServer(server => {
                server.Issuer = Vaultex;
                server.TokenEndpoint = $"{Vaultex}/connect/token";
                server.DeviceAuthorizationEndpoint = $"{Vaultex}/connect/device";
                server.JwksUri = $"{Vaultex}/.well-known/jwks.json";
                server.GrantTypesSupported.AddRange(["client_credentials", "urn:ietf:params:oauth:grant-type:device_code"]);
                server.ResponseTypesSupported.Add("code");
                server.TokenEndpointAuthMethodsSupported.Add("private_key_jwt");
                server.TokenEndpointAuthSigningAlgValuesSupported.Add("ES256");
                server.ProtectedResources.Add(Resource);
                server.CacheDuration = TimeSpan.FromHours(1);
            }),
            a => a.MapOAuthAuthorizationServer());

        await api.StartAsync(Ct);
        await vaultex.StartAsync(Ct);

        return new Deployment {
            Api = api,
            AuthorizationServer = vaultex,
            Network = new Network(new Dictionary<string, HttpMessageHandler> {
                ["api.example.com"] = api.GetTestServer().CreateHandler(),
                ["vaultex.example.com"] = vaultex.GetTestServer().CreateHandler()
            })
        };
    }

    [Fact]
    public async Task Should_Discover_The_Authorization_Server_From_A_401() {
        await using Deployment deployment = await DeployAsync();
        using HttpClient http = deployment.Client();
        OAuthDiscoveryClient discovery = new(deployment.Client(), new OAuthDiscoveryOptions { TrustedAuthorizationServers = { Vaultex } });

        using HttpResponseMessage unauthorized = await http.GetAsync(Resource, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        OAuthDiscoveryResult? result = await discovery.DiscoverAsync(unauthorized, Ct);

        Assert.NotNull(result);
        Assert.Equal(Resource, result.ProtectedResource.Resource);
        Assert.Equal(["keys:read"], result.ProtectedResource.ScopesSupported);
        Assert.Equal(Vaultex, result.AuthorizationServer.Issuer);
        Assert.Equal($"{Vaultex}/connect/token", result.AuthorizationServer.TokenEndpoint);
        Assert.Equal($"{Vaultex}/connect/device", result.AuthorizationServer.DeviceAuthorizationEndpoint);
        Assert.Equal(["private_key_jwt"], result.AuthorizationServer.TokenEndpointAuthMethodsSupported);
        Assert.Equal([Resource], result.AuthorizationServer.ProtectedResources);
    }

    [Fact]
    public async Task Should_Discover_From_The_Resource_Identifier_And_Serve_Repeats_From_Cache() {
        await using Deployment deployment = await DeployAsync();
        OAuthDiscoveryClient discovery = new(deployment.Client());

        await discovery.DiscoverAsync(Resource, Ct);
        OAuthDiscoveryResult result = await discovery.DiscoverAsync(Resource, Ct);

        Assert.Equal(Vaultex, result.AuthorizationServer.Issuer);
        Assert.Equal(1, deployment.Network.Requests[ProtectedResourceMetadataUri.For(Resource).AbsoluteUri]);
        Assert.Equal(1, deployment.Network.Requests[AuthorizationServerMetadataUri.For(Vaultex).AbsoluteUri]);
    }

    [Fact]
    public async Task Should_Refuse_A_Resource_That_Names_An_Untrusted_Authorization_Server() {
        await using Deployment deployment = await DeployAsync("https://attacker.example.com");
        using HttpClient http = deployment.Client();
        OAuthDiscoveryClient discovery = new(deployment.Client(), new OAuthDiscoveryOptions { TrustedAuthorizationServers = { Vaultex } });

        using HttpResponseMessage unauthorized = await http.GetAsync(Resource, Ct);

        OAuthDiscoveryException error = await Assert.ThrowsAsync<OAuthDiscoveryException>(() => discovery.DiscoverAsync(unauthorized, Ct));
        Assert.Equal(OAuthDiscoveryFailure.UntrustedAuthorizationServer, error.Failure);
        Assert.DoesNotContain(deployment.Network.Requests.Keys, url => url.StartsWith("https://attacker.example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_Refuse_A_401_From_A_Url_That_Is_Not_The_Resource_Identifier() {
        // RFC 9728 §3.3: the resource must be identical to the URL requested. The API's identifier is …/v1, so metadata
        // advertised on …/v1/keys is refused; a client that knows the identifier uses DiscoverAsync(resource).
        await using Deployment deployment = await DeployAsync();
        using HttpClient http = deployment.Client();
        OAuthDiscoveryClient discovery = new(deployment.Client());

        using HttpResponseMessage unauthorized = await http.GetAsync($"{Resource}/keys", Ct);

        OAuthDiscoveryException error = await Assert.ThrowsAsync<OAuthDiscoveryException>(() => discovery.DiscoverAsync(unauthorized, Ct));
        Assert.Equal(OAuthDiscoveryFailure.IdentifierMismatch, error.Failure);
    }

    [Fact]
    public async Task Should_Work_Through_The_Registered_Typed_Client() {
        await using Deployment deployment = await DeployAsync();
        ServiceCollection services = new();
        services.AddOAuthDiscoveryClient(o => o.TrustedAuthorizationServers.Add(Vaultex))
            .ConfigurePrimaryHttpMessageHandler(() => deployment.Network);

        await using ServiceProvider provider = services.BuildServiceProvider();
        OAuthDiscoveryResult result = await provider.GetRequiredService<OAuthDiscoveryClient>().DiscoverAsync(Resource, Ct);

        Assert.Equal($"{Vaultex}/connect/device", result.AuthorizationServer.DeviceAuthorizationEndpoint);
    }
}
