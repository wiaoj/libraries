using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Wiaoj.WellKnown.Tests.Integration;

/// <summary>
/// The served document conforms to RFC 9728 §2 and §3.2, and configuration a client would have to refuse fails at
/// startup (#101).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "WellKnown")]
[Trait("Component", "MetadataDocument")]
public sealed class MetadataDocumentTests {

    private static async Task<(HttpResponseMessage Response, JsonElement Document)> FetchAsync(
        Action<IServiceCollection> services, string path = ProtectedResourceMetadataUri.WellKnownPath) {

        await using TestApp app = await TestApp.StartAsync(services, a => a.MapOAuthProtectedResource());
        HttpResponseMessage response = await app.Client.GetAsync(path, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, JsonDocument.Parse(body).RootElement.Clone());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] Names(JsonElement document) => [.. document.EnumerateObject().Select(p => p.Name)];

    public sealed class TheDocument {
        [Fact]
        public async Task Should_Contain_Only_The_Resource_When_Nothing_Else_Is_Configured() {
            // Zero-valued parameters MUST be omitted (§3.2) — including the empty authorization_servers written before.
            (HttpResponseMessage response, JsonElement document) = await FetchAsync(s =>
                s.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(["resource"], Names(document));
            Assert.Equal("https://api.example.com", document.GetProperty("resource").GetString());
        }

        [Fact]
        public async Task Should_Publish_Every_Configured_Parameter_Under_Its_Rfc_Name() {
            (_, JsonElement document) = await FetchAsync(s => s.AddOAuthProtectedResource(r => {
                r.Resource = "https://api.example.com";
                r.AuthorizationServers.Add("https://auth.example.com");
                r.JwksUri = "https://api.example.com/jwks.json";
                r.Scopes.Add("assets:write");
                r.Scopes.Add("assets:read");
                r.BearerMethodsSupported.Add("header");
                r.ResourceSigningAlgValuesSupported.Add("RS256");
                r.ResourceName = "Example API";
                r.ResourceDocumentation = "https://docs.example.com";
                r.ResourcePolicyUri = "https://example.com/policy";
                r.ResourceTosUri = "https://example.com/tos";
                r.TlsClientCertificateBoundAccessTokens = true;
                r.AuthorizationDetailsTypesSupported.Add("payment_initiation");
                r.DpopSigningAlgValuesSupported.Add("ES256");
                r.DpopBoundAccessTokensRequired = true;
            }));

            Assert.Equal(
                [
                    "resource", "authorization_servers", "jwks_uri", "scopes_supported", "bearer_methods_supported",
                    "resource_signing_alg_values_supported", "resource_name", "resource_documentation", "resource_policy_uri",
                    "resource_tos_uri", "tls_client_certificate_bound_access_tokens", "authorization_details_types_supported",
                    "dpop_signing_alg_values_supported", "dpop_bound_access_tokens_required"
                ],
                Names(document));

            Assert.Equal(["assets:read", "assets:write"], document.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
            Assert.True(document.GetProperty("dpop_bound_access_tokens_required").GetBoolean());
        }

        [Fact]
        public async Task Should_Merge_Scopes_Added_By_Modules_Without_Duplicates() {
            (_, JsonElement document) = await FetchAsync(s => {
                s.AddOAuthProtectedResource(r => {
                    r.Resource = "https://api.example.com";
                    r.Scopes.Add("keys:read");
                });
                s.AddProtectedResourceScopes(" keys:write ", "keys:read", "");
                s.AddProtectedResourceScopes("audit:read");
            });

            Assert.Equal(["audit:read", "keys:read", "keys:write"], document.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
        }

        [Fact]
        public async Task Should_Treat_Several_String_Arguments_As_Scopes_Not_As_A_Resource_Name() {
            // With a params overload taking a name first, the first scope bound as the name and the rest went to a resource
            // nobody registered — every module's scopes silently missing from the document.
            (_, JsonElement document) = await FetchAsync(s => {
                s.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com");
                s.AddProtectedResourceScopes("a:read", "a:write");
            });

            Assert.Equal(["a:read", "a:write"], document.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
        }

        [Fact]
        public async Task Should_Publish_Additional_Parameters_As_Top_Level_Members() {
            (_, JsonElement document) = await FetchAsync(s => s.AddOAuthProtectedResource(r => {
                r.Resource = "https://api.example.com";
                r.AdditionalParameters["tenant_id"] = "acme";
                r.AdditionalParameters["limits"] = new System.Text.Json.Nodes.JsonObject { ["rpm"] = 600 };
            }));

            Assert.Equal("acme", document.GetProperty("tenant_id").GetString());
            Assert.Equal(600, document.GetProperty("limits").GetProperty("rpm").GetInt32());
            Assert.Equal(["resource", "tenant_id", "limits"], Names(document));
        }

        [Fact]
        public async Task Should_Serve_Additional_Parameters_On_Every_Request_Not_Only_The_First() {
            // A JSON node can have one parent; attaching the options' own nodes would fail from the second request on.
            await using TestApp app = await TestApp.StartAsync(
                s => s.AddOAuthProtectedResource(r => {
                    r.Resource = "https://api.example.com";
                    r.AdditionalParameters["tier"] = new System.Text.Json.Nodes.JsonObject { ["name"] = "enterprise" };
                }),
                a => a.MapOAuthProtectedResource());

            for(int i = 0; i < 3; i++) {
                HttpResponseMessage response = await app.Client.GetAsync(ProtectedResourceMetadataUri.WellKnownPath, Ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task Should_Allow_Caching_For_The_Configured_Duration() {
            (HttpResponseMessage response, _) = await FetchAsync(s => s.AddOAuthProtectedResource(r => {
                r.Resource = "https://api.example.com";
                r.CacheDuration = TimeSpan.FromMinutes(5);
            }));

            Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        }

        [Fact]
        public async Task Should_Not_Allow_Caching_When_The_Duration_Is_Zero() {
            (HttpResponseMessage response, _) = await FetchAsync(s => s.AddOAuthProtectedResource(r => {
                r.Resource = "https://api.example.com";
                r.CacheDuration = TimeSpan.Zero;
            }));

            Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        }

        [Fact]
        public async Task Should_Allow_Anonymous_Access_When_Authorization_Is_The_Default() {
            await using TestApp app = await TestApp.StartAsync(
                s => {
                    s.AddAuthentication();
                    s.AddAuthorizationBuilder().SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
                    s.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com");
                },
                a => {
                    a.UseAuthentication();
                    a.UseAuthorization();
                    a.MapOAuthProtectedResource();
                });

            HttpResponseMessage response = await app.Client.GetAsync(ProtectedResourceMetadataUri.WellKnownPath, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public sealed class StartupValidation {
        private static async Task<OptionsValidationException> StartFailureAsync(Action<OAuthProtectedResourceOptions> configure) {
            WebApplication app = TestApp.Build(s => s.AddOAuthProtectedResource(configure), _ => { });
            await using WebApplication _ = app;
            return await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Require_A_Resource_And_Never_Derive_It_From_The_Request() {
            OptionsValidationException error = await StartFailureAsync(_ => { });

            Assert.Contains("has no Resource", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("http://api.example.com", "must use https")]
        [InlineData("/relative", "not an absolute URL")]
        [InlineData("https://api.example.com/#section", "query or fragment")]
        [InlineData("https://api.example.com/?tenant=1", "query or fragment")]
        [InlineData("https://user:secret@api.example.com", "user information")]
        public async Task Should_Refuse_A_Resource_Identifier_Clients_Cannot_Match(string resource, string reason) {
            OptionsValidationException error = await StartFailureAsync(r => r.Resource = resource);

            Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("http://localhost:5000")]
        [InlineData("http://127.0.0.1:5000/api")]
        public async Task Should_Accept_Http_On_A_Loopback_Host_For_Development(string resource) {
            await using TestApp app = await TestApp.StartAsync(s => s.AddOAuthProtectedResource(r => r.Resource = resource), a => a.MapOAuthProtectedResource());
        }

        [Fact]
        public async Task Should_Report_Every_Problem_At_Once() {
            OptionsValidationException error = await StartFailureAsync(r => {
                r.Resource = "https://api.example.com";
                r.AuthorizationServers.Add("auth.example.com");
                r.JwksUri = "http://keys.example.com/jwks";
                r.ResourceSigningAlgValuesSupported.Add("none");
                r.DpopSigningAlgValuesSupported.Add("none");
                r.BearerMethodsSupported.Add("cookie");
                r.Scopes.Add("has space");
                r.ResourceTosUri = "not a url";
                r.CacheDuration = TimeSpan.FromSeconds(-1);
            });

            Assert.Equal(8, error.Failures.Count());
            Assert.Contains(error.Failures, f => f.Contains("authorization server 'auth.example.com'", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("jwks_uri", StringComparison.Ordinal) && f.Contains("must use https", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("resource_signing_alg_values_supported contains 'none'", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("dpop_signing_alg_values_supported contains 'none'", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("bearer method 'cookie'", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("scope 'has space'", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("resource_tos_uri", StringComparison.Ordinal));
            Assert.Contains(error.Failures, f => f.Contains("CacheDuration is negative", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("resource")]
        [InlineData("scopes_supported")]
        [InlineData("signed_metadata")]
        public async Task Should_Refuse_An_Additional_Parameter_That_Reuses_A_Standard_Name(string name) {
            // It would publish, unchecked, a value the options otherwise validate.
            OptionsValidationException error = await StartFailureAsync(r => {
                r.Resource = "https://api.example.com";
                r.AdditionalParameters[name] = "https://attacker.example";
            });

            Assert.Contains($"additional parameter '{name}' is defined by RFC 9728", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Not_Validate_Options_Requested_Under_A_Name_Nobody_Registered() {
            ServiceCollection services = new();
            services.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com");
            using ServiceProvider provider = services.BuildServiceProvider();

            OAuthProtectedResourceOptions unrelated = provider.GetRequiredService<IOptionsMonitor<OAuthProtectedResourceOptions>>().Get("unregistered");

            Assert.Null(unrelated.Resource);
        }
    }
}
