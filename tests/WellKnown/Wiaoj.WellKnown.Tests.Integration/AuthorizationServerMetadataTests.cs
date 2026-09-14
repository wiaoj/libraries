using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wiaoj.WellKnown.Tests.Integration;

/// <summary>
/// The served document conforms to RFC 8414 §2, §3 and §3.2, and configuration a client would have to refuse or would
/// misread fails at startup (#113).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "WellKnown")]
[Trait("Component", "AuthorizationServerMetadata")]
public sealed class AuthorizationServerMetadataTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] Names(JsonElement document) => [.. document.EnumerateObject().Select(p => p.Name)];

    /// <summary>The smallest valid server: a service-to-service issuer with no authorization endpoint.</summary>
    private static void Minimal(OAuthAuthorizationServerOptions server) {
        server.Issuer = "https://auth.example.com";
        server.TokenEndpoint = "https://auth.example.com/token";
        server.GrantTypesSupported.Add("client_credentials");
        server.ResponseTypesSupported.Add("code");
    }

    private static async Task<(HttpResponseMessage Response, JsonElement Document)> FetchAsync(
        Action<IServiceCollection> services, string path = AuthorizationServerMetadataUri.WellKnownPath) {

        await using TestApp app = await TestApp.StartAsync(services, a => a.MapOAuthAuthorizationServer());
        HttpResponseMessage response = await app.Client.GetAsync(path, Ct);
        string body = await response.Content.ReadAsStringAsync(Ct);
        return (response, response.IsSuccessStatusCode ? JsonDocument.Parse(body).RootElement.Clone() : default);
    }

    public sealed class TheDocument {
        [Fact]
        public async Task Should_Contain_Only_The_Configured_Parameters() {
            (HttpResponseMessage response, JsonElement document) = await FetchAsync(s => s.AddOAuthAuthorizationServer(Minimal));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(["issuer", "token_endpoint", "response_types_supported", "grant_types_supported"], Names(document));
            Assert.Equal("https://auth.example.com", document.GetProperty("issuer").GetString());
        }

        [Fact]
        public async Task Should_Publish_Every_Typed_Parameter_Under_Its_Registered_Name() {
            (_, JsonElement document) = await FetchAsync(s => s.AddOAuthAuthorizationServer(a => {
                a.Issuer = "https://auth.example.com";
                a.AuthorizationEndpoint = "https://auth.example.com/authorize";
                a.TokenEndpoint = "https://auth.example.com/token";
                a.JwksUri = "https://auth.example.com/jwks";
                a.RegistrationEndpoint = "https://auth.example.com/register";
                a.Scopes.Add("vault:write");
                a.Scopes.Add("vault:read");
                a.ResponseTypesSupported.Add("code");
                a.ResponseModesSupported.Add("query");
                a.GrantTypesSupported.Add("authorization_code");
                a.TokenEndpointAuthMethodsSupported.Add("private_key_jwt");
                a.TokenEndpointAuthSigningAlgValuesSupported.Add("ES256");
                a.ServiceDocumentation = "https://docs.example.com";
                a.UiLocalesSupported.Add("tr-TR");
                a.OpPolicyUri = "https://example.com/policy";
                a.OpTosUri = "https://example.com/tos";
                a.RevocationEndpoint = "https://auth.example.com/revoke";
                a.RevocationEndpointAuthMethodsSupported.Add("client_secret_jwt");
                a.RevocationEndpointAuthSigningAlgValuesSupported.Add("HS256");
                a.IntrospectionEndpoint = "https://auth.example.com/introspect";
                a.IntrospectionEndpointAuthMethodsSupported.Add("client_secret_basic");
                a.IntrospectionEndpointAuthSigningAlgValuesSupported.Add("RS256");
                a.CodeChallengeMethodsSupported.Add("S256");
                a.DeviceAuthorizationEndpoint = "https://auth.example.com/device";
                a.PushedAuthorizationRequestEndpoint = "https://auth.example.com/par";
                a.RequirePushedAuthorizationRequests = true;
                a.AuthorizationResponseIssParameterSupported = true;
                a.DpopSigningAlgValuesSupported.Add("ES256");
                a.TlsClientCertificateBoundAccessTokens = true;
                a.ProtectedResources.Add("https://api.example.com");
            }));

            Assert.Equal(
                [
                    "issuer", "authorization_endpoint", "token_endpoint", "jwks_uri", "registration_endpoint", "scopes_supported",
                    "response_types_supported", "response_modes_supported", "grant_types_supported",
                    "token_endpoint_auth_methods_supported", "token_endpoint_auth_signing_alg_values_supported",
                    "service_documentation", "ui_locales_supported", "op_policy_uri", "op_tos_uri", "revocation_endpoint",
                    "revocation_endpoint_auth_methods_supported", "revocation_endpoint_auth_signing_alg_values_supported",
                    "introspection_endpoint", "introspection_endpoint_auth_methods_supported",
                    "introspection_endpoint_auth_signing_alg_values_supported", "code_challenge_methods_supported",
                    "device_authorization_endpoint", "pushed_authorization_request_endpoint",
                    "require_pushed_authorization_requests", "authorization_response_iss_parameter_supported",
                    "dpop_signing_alg_values_supported", "tls_client_certificate_bound_access_tokens", "protected_resources"
                ],
                Names(document));

            Assert.Equal(["vault:read", "vault:write"], document.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
            Assert.True(document.GetProperty("require_pushed_authorization_requests").GetBoolean());
        }

        [Fact]
        public void Should_Name_Every_Serialized_Parameter_As_Standard() {
            // An additional parameter reusing any of these would overwrite a validated value.
            foreach(System.Reflection.PropertyInfo property in typeof(OAuthAuthorizationServerMetadata).GetProperties()) {
                if(property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false) is [System.Text.Json.Serialization.JsonPropertyNameAttribute name]) {
                    Assert.Contains(name.Name, OAuthAuthorizationServerMetadata.StandardParameterNames);
                }
            }
        }

        [Fact]
        public async Task Should_Publish_Additional_Parameters_On_Every_Request() {
            await using TestApp app = await TestApp.StartAsync(
                s => s.AddOAuthAuthorizationServer(a => {
                    Minimal(a);
                    a.AdditionalParameters["userinfo_endpoint"] = "https://auth.example.com/userinfo";
                    a.AdditionalParameters["mtls_endpoint_aliases"] = new JsonObject { ["token_endpoint"] = "https://mtls.auth.example.com/token" };
                }),
                a => a.MapOAuthAuthorizationServer());

            for(int i = 0; i < 3; i++) {
                string body = await app.Client.GetStringAsync(AuthorizationServerMetadataUri.WellKnownPath, Ct);
                JsonElement document = JsonDocument.Parse(body).RootElement;

                Assert.Equal("https://auth.example.com/userinfo", document.GetProperty("userinfo_endpoint").GetString());
                Assert.Equal("https://mtls.auth.example.com/token", document.GetProperty("mtls_endpoint_aliases").GetProperty("token_endpoint").GetString());
            }
        }

        [Fact]
        public async Task Should_Allow_Caching_For_The_Configured_Duration() {
            (HttpResponseMessage response, _) = await FetchAsync(s => s.AddOAuthAuthorizationServer(a => {
                Minimal(a);
                a.CacheDuration = TimeSpan.FromMinutes(10);
            }));

            Assert.Equal("public, max-age=600", response.Headers.CacheControl?.ToString());
            Assert.NotNull(response.Content.Headers.ContentLength);
        }

        [Fact]
        public async Task Should_Serve_An_Issuer_With_A_Path_Under_The_Path_Without_Its_Terminating_Slash() {
            (HttpResponseMessage response, JsonElement document) = await FetchAsync(
                s => s.AddOAuthAuthorizationServer(a => {
                    Minimal(a);
                    a.Issuer = "https://auth.example.com/tenant1/";
                }),
                "/.well-known/oauth-authorization-server/tenant1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("https://auth.example.com/tenant1/", document.GetProperty("issuer").GetString());
        }

        [Fact]
        public async Task Should_Serve_Each_Issuer_On_A_Shared_Host_At_Its_Own_Path() {
            await using TestApp app = await TestApp.StartAsync(
                s => {
                    s.AddOAuthAuthorizationServer("one", a => { Minimal(a); a.Issuer = "https://auth.example.com/one"; });
                    s.AddOAuthAuthorizationServer("two", a => { Minimal(a); a.Issuer = "https://auth.example.com/two"; });
                },
                a => a.MapOAuthAuthorizationServer());

            foreach(string tenant in new[] { "one", "two" }) {
                string body = await app.Client.GetStringAsync($"/.well-known/oauth-authorization-server/{tenant}", Ct);
                Assert.Equal($"https://auth.example.com/{tenant}", JsonDocument.Parse(body).RootElement.GetProperty("issuer").GetString());
            }
        }

        [Fact]
        public async Task Should_Refuse_Two_Issuers_That_Differ_Only_By_A_Terminating_Slash() {
            WebApplication app = TestApp.Build(
                s => {
                    s.AddOAuthAuthorizationServer("bare", a => { Minimal(a); a.Issuer = "https://auth.example.com/t"; });
                    s.AddOAuthAuthorizationServer("slash", a => { Minimal(a); a.Issuer = "https://auth.example.com/t/"; });
                },
                _ => { });
            await using WebApplication _ = app;

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => app.MapOAuthAuthorizationServer());
            Assert.Contains("'bare' and 'slash'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Refuse_To_Map_When_No_Server_Is_Registered() {
            WebApplication app = TestApp.Build(_ => { }, _ => { });
            await using WebApplication _ = app;

            Assert.Throws<InvalidOperationException>(() => app.MapOAuthAuthorizationServer());
        }
    }

    public sealed class TheMetadataUrl {
        [Theory]
        [InlineData("https://auth.example.com", "https://auth.example.com/.well-known/oauth-authorization-server")]
        [InlineData("https://auth.example.com/", "https://auth.example.com/.well-known/oauth-authorization-server")]
        [InlineData("https://auth.example.com/issuer1", "https://auth.example.com/.well-known/oauth-authorization-server/issuer1")]
        [InlineData("https://auth.example.com/issuer1/", "https://auth.example.com/.well-known/oauth-authorization-server/issuer1")]
        [InlineData("https://auth.example.com:8443/a/b", "https://auth.example.com:8443/.well-known/oauth-authorization-server/a/b")]
        public void Should_Insert_The_Suffix_And_Remove_The_Terminating_Slash(string issuer, string expected) {
            // RFC 8414 §3.1 — unlike RFC 9728, which keeps a path's own trailing slash.
            Assert.Equal(expected, AuthorizationServerMetadataUri.For(issuer).AbsoluteUri);
        }

        [Fact]
        public void Should_Keep_The_Protected_Resource_Derivation_Unchanged() {
            Assert.Equal(
                "https://api.example.com/.well-known/oauth-protected-resource/v1/",
                ProtectedResourceMetadataUri.For("https://api.example.com/v1/").AbsoluteUri);
        }

        [Theory]
        [InlineData("https://auth.example.com?tenant=1")]
        [InlineData("https://auth.example.com#x")]
        [InlineData("relative")]
        public void Should_Refuse_An_Issuer_That_Cannot_Derive_A_Location(string issuer) {
            Assert.Throws<ArgumentException>(() => AuthorizationServerMetadataUri.PathFor(issuer));
        }
    }

    public sealed class StartupValidation {
        private static async Task<OptionsValidationException> StartFailureAsync(Action<OAuthAuthorizationServerOptions> configure) {
            WebApplication app = TestApp.Build(s => s.AddOAuthAuthorizationServer(configure), _ => { });
            await using WebApplication _ = app;
            return await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(Ct));
        }

        private static async Task StartsAsync(Action<OAuthAuthorizationServerOptions> configure) {
            await using TestApp app = await TestApp.StartAsync(s => s.AddOAuthAuthorizationServer(configure), a => a.MapOAuthAuthorizationServer());
        }

        [Fact]
        public async Task Should_Accept_The_Minimal_Server() {
            await StartsAsync(Minimal);
        }

        [Fact]
        public async Task Should_Require_An_Issuer() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.Issuer = null; });

            Assert.Contains("has no Issuer", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("http://auth.example.com", "must use https")]
        [InlineData("/relative", "not an absolute URL")]
        [InlineData("https://auth.example.com/#x", "query or fragment")]
        [InlineData("https://auth.example.com/?tenant=1", "query or fragment")]
        [InlineData("https://user:secret@auth.example.com", "user information")]
        public async Task Should_Refuse_An_Issuer_Clients_Cannot_Match(string issuer, string reason) {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.Issuer = issuer; });

            Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Accept_Http_On_A_Loopback_Host_For_Development() {
            await StartsAsync(a => {
                Minimal(a);
                a.Issuer = "http://localhost:5001";
                a.TokenEndpoint = "http://localhost:5001/token";
            });
        }

        [Fact]
        public async Task Should_Require_Response_Types() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.ResponseTypesSupported.Clear(); });

            Assert.Contains("response_types_supported", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Require_Both_Endpoints_When_Grant_Types_Are_Left_To_The_Default() {
            // Omitted grant_types_supported means authorization_code and implicit to a client, not "none".
            OptionsValidationException error = await StartFailureAsync(a => {
                a.Issuer = "https://auth.example.com";
                a.ResponseTypesSupported.Add("code");
            });

            Assert.Contains("but no AuthorizationEndpoint", error.Message, StringComparison.Ordinal);
            Assert.Contains("but no TokenEndpoint", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Not_Require_An_Authorization_Endpoint_For_Grants_That_Do_Not_Use_It() {
            await StartsAsync(a => {
                Minimal(a);
                a.GrantTypesSupported.Add("urn:ietf:params:oauth:grant-type:device_code");
                a.DeviceAuthorizationEndpoint = "https://auth.example.com/device";
            });
        }

        [Fact]
        public async Task Should_Require_An_Authorization_Endpoint_For_The_Authorization_Code_Grant() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.GrantTypesSupported.Add("authorization_code"); });

            Assert.Contains("but no AuthorizationEndpoint", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("but no TokenEndpoint", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Not_Require_A_Token_Endpoint_When_Only_The_Implicit_Grant_Is_Supported() {
            await StartsAsync(a => {
                a.Issuer = "https://auth.example.com";
                a.AuthorizationEndpoint = "https://auth.example.com/authorize";
                a.GrantTypesSupported.Add("implicit");
                a.ResponseTypesSupported.Add("token");
            });
        }

        [Theory]
        [InlineData("token_endpoint")]
        [InlineData("revocation_endpoint")]
        [InlineData("introspection_endpoint")]
        public async Task Should_Require_Signing_Algorithms_When_A_Jwt_Authentication_Method_Is_Supported(string endpoint) {
            OptionsValidationException error = await StartFailureAsync(a => {
                Minimal(a);
                Methods(a, endpoint).Add("private_key_jwt");
            });

            Assert.Contains($"lists no {endpoint}_auth_signing_alg_values_supported", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("token_endpoint")]
        [InlineData("revocation_endpoint")]
        [InlineData("introspection_endpoint")]
        public async Task Should_Refuse_None_As_A_Client_Authentication_Algorithm(string endpoint) {
            OptionsValidationException error = await StartFailureAsync(a => {
                Minimal(a);
                Methods(a, endpoint).Add("client_secret_jwt");
                Algorithms(a, endpoint).AddRange(["HS256", "none"]);
            });

            Assert.Contains($"{endpoint}_auth_signing_alg_values_supported contains 'none'", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("lists no", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Accept_Jwt_Authentication_With_Its_Algorithms() {
            await StartsAsync(a => {
                Minimal(a);
                a.TokenEndpointAuthMethodsSupported.AddRange(["client_secret_basic", "private_key_jwt"]);
                a.TokenEndpointAuthSigningAlgValuesSupported.Add("ES256");
            });
        }

        [Fact]
        public async Task Should_Refuse_None_As_A_DPoP_Algorithm() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.DpopSigningAlgValuesSupported.Add("none"); });

            Assert.Contains("dpop_signing_alg_values_supported contains 'none'", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("http://auth.example.com/token", "token_endpoint", "must use https")]
        [InlineData("/token", "token_endpoint", "is not an absolute http or https URL")]
        [InlineData("http://auth.example.com/device", "device_authorization_endpoint", "must use https")]
        [InlineData("ftp://auth.example.com/jwks", "jwks_uri", "is not an absolute http or https URL")]
        public async Task Should_Refuse_An_Endpoint_That_Is_Not_Absolute_Https(string url, string parameter, string reason) {
            OptionsValidationException error = await StartFailureAsync(a => {
                Minimal(a);
                switch(parameter) {
                    case "token_endpoint": a.TokenEndpoint = url; break;
                    case "device_authorization_endpoint": a.DeviceAuthorizationEndpoint = url; break;
                    default: a.JwksUri = url; break;
                }
            });

            Assert.Contains($"{parameter} '{url}' {reason}", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Accept_Http_For_Human_Readable_Pages() {
            // RFC 8414's own example publishes service_documentation over http.
            await StartsAsync(a => {
                Minimal(a);
                a.ServiceDocumentation = "http://docs.example.com";
                a.OpPolicyUri = "http://example.com/policy";
                a.OpTosUri = "http://example.com/tos";
            });
        }

        [Fact]
        public async Task Should_Refuse_Requiring_Pushed_Requests_Without_The_Endpoint() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.RequirePushedAuthorizationRequests = true; });

            Assert.Contains("no PushedAuthorizationRequestEndpoint", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Refuse_An_Invalid_Scope_Token() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.Scopes.Add("vault read"); });

            Assert.Contains("scope 'vault read' is not a valid scope token", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("http://api.example.com", "must use https")]
        [InlineData("https://api.example.com#x", "query or fragment")]
        [InlineData(" ", "An entry of ProtectedResources is blank")]
        public async Task Should_Refuse_A_Protected_Resource_That_Is_Not_A_Resource_Identifier(string resource, string reason) {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.ProtectedResources.Add(resource); });

            Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("token_endpoint")]
        [InlineData("device_authorization_endpoint")]
        [InlineData("protected_resources")]
        [InlineData("signed_metadata")]
        public async Task Should_Refuse_An_Additional_Parameter_That_Has_A_Typed_Option(string parameter) {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.AdditionalParameters[parameter] = "https://evil.example.com"; });

            Assert.Contains($"additional parameter '{parameter}' is defined by RFC 8414", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Refuse_A_Negative_Cache_Duration() {
            OptionsValidationException error = await StartFailureAsync(a => { Minimal(a); a.CacheDuration = TimeSpan.FromSeconds(-1); });

            Assert.Contains("CacheDuration is negative", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Report_Every_Failure_At_Once() {
            OptionsValidationException error = await StartFailureAsync(a => {
                a.Issuer = "http://auth.example.com";
                a.JwksUri = "http://auth.example.com/jwks";
            });

            Assert.True(error.Failures.Count() >= 5, string.Join(Environment.NewLine, error.Failures));
        }

        [Fact]
        public async Task Should_Name_The_Server_In_Failures_Of_A_Named_Registration() {
            WebApplication app = TestApp.Build(s => s.AddOAuthAuthorizationServer("vaultex", a => { Minimal(a); a.Issuer = null; }), _ => { });
            await using WebApplication _ = app;

            OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(Ct));
            Assert.Contains("The authorization server 'vaultex' has no Issuer", error.Message, StringComparison.Ordinal);
        }

        private static List<string> Methods(OAuthAuthorizationServerOptions a, string endpoint) => endpoint switch {
            "token_endpoint" => a.TokenEndpointAuthMethodsSupported,
            "revocation_endpoint" => a.RevocationEndpointAuthMethodsSupported,
            _ => a.IntrospectionEndpointAuthMethodsSupported
        };

        private static List<string> Algorithms(OAuthAuthorizationServerOptions a, string endpoint) => endpoint switch {
            "token_endpoint" => a.TokenEndpointAuthSigningAlgValuesSupported,
            "revocation_endpoint" => a.RevocationEndpointAuthSigningAlgValuesSupported,
            _ => a.IntrospectionEndpointAuthSigningAlgValuesSupported
        };
    }
}
