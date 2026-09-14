using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;

namespace Wiaoj.WellKnown.Discovery.Tests.Unit;

/// <summary>
/// Discovery refuses every document RFC 9728 §3.3 and RFC 8414 §3.3 forbid using, fetches only securely, and caches no
/// longer than allowed (#114).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "WellKnown.Discovery")]
[Trait("Component", "OAuthDiscoveryClient")]
public sealed class OAuthDiscoveryClientTests {
    private const string Api = "https://api.example.com/v1";
    private const string ApiMetadata = "https://api.example.com/.well-known/oauth-protected-resource/v1";
    private const string Vaultex = "https://vaultex.example.com";
    private const string VaultexMetadata = "https://vaultex.example.com/.well-known/oauth-authorization-server";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OAuthDiscoveryClient Client(FakeMetadataServer server, Action<OAuthDiscoveryOptions>? configure = null, TimeProvider? time = null) {
        OAuthDiscoveryOptions options = new();
        configure?.Invoke(options);
        return new OAuthDiscoveryClient(new HttpClient(server), options, time);
    }

    private static async Task<OAuthDiscoveryFailure> FailureAsync(Func<Task> act) {
        return (await Assert.ThrowsAsync<OAuthDiscoveryException>(act)).Failure;
    }

    public sealed class ProtectedResourceMetadata {
        [Fact]
        public async Task Should_Fetch_From_The_Derived_Url_And_Return_The_Document() {
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, """{"resource":"https://api.example.com/v1","scopes_supported":["a"],"custom":{"x":1}}""");

            ProtectedResourceMetadataDocument document = await Client(server).GetProtectedResourceMetadataAsync(Api, Ct);

            Assert.Equal(Api, document.Resource);
            Assert.Equal(["a"], document.ScopesSupported);
            Assert.Empty(document.AuthorizationServers);
            Assert.Equal(1, document.Json.GetProperty("custom").GetProperty("x").GetInt32());
            Assert.Equal(new Uri(ApiMetadata), document.Source);
        }

        [Theory]
        [InlineData("https://api.example.com/v1/")]
        [InlineData("https://API.example.com/v1")]
        [InlineData("https://api.example.com/V1")]
        [InlineData("https://attacker.example.com/v1")]
        public async Task Should_Refuse_A_Document_For_Any_Other_Identifier(string claimed) {
            // §3.3 and §6: identical, code point for code point — no normalisation.
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(claimed));

            Assert.Equal(OAuthDiscoveryFailure.IdentifierMismatch, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(Api, Ct)));
        }

        [Fact]
        public async Task Should_Refuse_A_Cached_Document_For_Another_Identifier_Too() {
            // Validation runs on every call, not only when the document is fetched.
            FakeMetadataServer server = new FakeMetadataServer().Json("https://api.example.com/.well-known/oauth-protected-resource", FakeMetadataServer.Resource("https://api.example.com"));
            OAuthDiscoveryClient client = Client(server);

            await client.GetProtectedResourceMetadataAsync("https://api.example.com", Ct);

            Assert.Equal(OAuthDiscoveryFailure.IdentifierMismatch, await FailureAsync(() => client.GetProtectedResourceMetadataAsync("https://api.example.com/", Ct)));
            Assert.Equal(1, server.TotalHits);
        }

        [Theory]
        [InlineData("http://api.example.com", OAuthDiscoveryFailure.InsecureTransport)]
        [InlineData("https://api.example.com?x=1", OAuthDiscoveryFailure.InvalidUrl)]
        [InlineData("not a url", OAuthDiscoveryFailure.InvalidUrl)]
        public async Task Should_Refuse_An_Identifier_It_Cannot_Fetch_Securely(string resource, OAuthDiscoveryFailure failure) {
            FakeMetadataServer server = new();

            Assert.Equal(failure, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(resource, Ct)));
            Assert.Equal(0, server.TotalHits);
        }

        [Fact]
        public async Task Should_Allow_Http_On_Loopback_Unless_Disabled() {
            FakeMetadataServer server = new FakeMetadataServer().Json("http://localhost:5000/.well-known/oauth-protected-resource", FakeMetadataServer.Resource("http://localhost:5000"));

            await Client(server).GetProtectedResourceMetadataAsync("http://localhost:5000", Ct);

            Assert.Equal(
                OAuthDiscoveryFailure.InsecureTransport,
                await FailureAsync(() => Client(server, o => o.AllowHttpOnLoopback = false).GetProtectedResourceMetadataAsync("http://localhost:5000", Ct)));
        }
    }

    public sealed class Transport {
        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task Should_Refuse_Anything_But_200(HttpStatusCode status) {
            FakeMetadataServer server = new FakeMetadataServer().Route(ApiMetadata, _ => new HttpResponseMessage(status));

            Assert.Equal(OAuthDiscoveryFailure.HttpError, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(Api, Ct)));
        }

        [Fact]
        public async Task Should_Report_A_Network_Failure_As_Discovery_Failure() {
            FakeMetadataServer server = new FakeMetadataServer().Route(ApiMetadata, _ => throw new HttpRequestException("connection refused"));

            Assert.Equal(OAuthDiscoveryFailure.HttpError, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(Api, Ct)));
        }

        [Fact]
        public async Task Should_Follow_A_Same_Origin_Redirect() {
            FakeMetadataServer server = new FakeMetadataServer()
                .Route(ApiMetadata, _ => Redirect("/moved"))
                .Json("https://api.example.com/moved", FakeMetadataServer.Resource(Api));

            ProtectedResourceMetadataDocument document = await Client(server).GetProtectedResourceMetadataAsync(Api, Ct);

            Assert.Equal(Api, document.Resource);
        }

        [Theory]
        [InlineData("https://attacker.example.com/.well-known/oauth-protected-resource/v1")]
        [InlineData("http://api.example.com/.well-known/oauth-protected-resource/v1")]
        [InlineData("https://api.example.com:8443/.well-known/oauth-protected-resource/v1")]
        public async Task Should_Never_Follow_A_Redirect_To_Another_Origin(string target) {
            FakeMetadataServer server = new FakeMetadataServer()
                .Route(ApiMetadata, _ => Redirect(target))
                .Json(target, FakeMetadataServer.Resource(Api));

            Assert.Equal(OAuthDiscoveryFailure.CrossOriginRedirect, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(Api, Ct)));
            Assert.Equal(0, server.Hits(target));
        }

        [Fact]
        public async Task Should_Refuse_A_Response_A_Handler_Reached_Through_Another_Origin() {
            // A handler with AllowAutoRedirect follows the redirect before the client sees it; the request it ended on shows it.
            FakeMetadataServer server = new FakeMetadataServer().Route(ApiMetadata, request => {
                request.RequestUri = new Uri("https://attacker.example.com/doc");
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(FakeMetadataServer.Resource(Api), Encoding.UTF8, "application/json"),
                    RequestMessage = request
                };
            });

            Assert.Equal(OAuthDiscoveryFailure.CrossOriginRedirect, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(Api, Ct)));
        }

        [Fact]
        public async Task Should_Stop_After_The_Configured_Number_Of_Redirects() {
            FakeMetadataServer server = new FakeMetadataServer()
                .Route(ApiMetadata, _ => Redirect("/r1"))
                .Route("https://api.example.com/r1", _ => Redirect("/r2"))
                .Route("https://api.example.com/r2", _ => Redirect("/r3"))
                .Json("https://api.example.com/r3", FakeMetadataServer.Resource(Api));

            Assert.Equal(OAuthDiscoveryFailure.TooManyRedirects, await FailureAsync(() => Client(server, o => o.MaxRedirects = 2).GetProtectedResourceMetadataAsync(Api, Ct)));
            Assert.Equal(Api, (await Client(server, o => o.MaxRedirects = 3).GetProtectedResourceMetadataAsync(Api, Ct)).Resource);
        }

        [Theory]
        [InlineData("text/html", "{\"resource\":\"https://api.example.com/v1\"}")]
        [InlineData("application/json", "[\"https://api.example.com/v1\"]")]
        [InlineData("application/json", "{\"resource\":")]
        [InlineData("application/json", "{\"resource\":42}")]
        [InlineData("application/json", "{}")]
        [InlineData("application/json", "{\"resource\":\"https://api.example.com/v1\",\"authorization_servers\":\"https://vaultex.example.com\"}")]
        [InlineData("application/json", "{\"resource\":\"https://api.example.com/v1\",\"authorization_servers\":[1]}")]
        [InlineData("application/json", "{\"resource\":\"https://api.example.com/v1\",\"dpop_bound_access_tokens_required\":\"yes\"}")]
        public async Task Should_Refuse_A_Document_That_Is_Not_Valid_Metadata(string contentType, string body) {
            FakeMetadataServer server = new FakeMetadataServer().Route(ApiMetadata, _ => new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(body, Encoding.UTF8, contentType)
            });

            Assert.Equal(OAuthDiscoveryFailure.InvalidDocument, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(Api, Ct)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Should_Refuse_A_Document_Larger_Than_The_Limit(bool declaredLength) {
            string body = FakeMetadataServer.Resource(Api)[..^1] + ",\"padding\":\"" + new string('x', 2000) + "\"}";
            FakeMetadataServer server = new FakeMetadataServer().Route(ApiMetadata, _ => {
                HttpContent content = declaredLength
                    ? new StringContent(body, Encoding.UTF8, "application/json")
                    : new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body)));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                if(!declaredLength) {
                    content.Headers.ContentLength = null;
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });

            Assert.Equal(OAuthDiscoveryFailure.DocumentTooLarge, await FailureAsync(() => Client(server, o => o.MaxDocumentBytes = 1000).GetProtectedResourceMetadataAsync(Api, Ct)));
            Assert.Equal(Api, (await Client(server, o => o.MaxDocumentBytes = 4000).GetProtectedResourceMetadataAsync(Api, Ct)).Resource);
        }

        private static HttpResponseMessage Redirect(string location) {
            HttpResponseMessage response = new(HttpStatusCode.Found);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        }
    }

    public sealed class Caching {
        [Fact]
        public async Task Should_Serve_From_Cache_Until_Max_Age_And_Never_After() {
            FakeTimeProvider time = new();
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(Api), "public, max-age=60");
            OAuthDiscoveryClient client = Client(server, time: time);

            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            time.Advance(TimeSpan.FromSeconds(59));
            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            Assert.Equal(1, server.Hits(ApiMetadata));

            time.Advance(TimeSpan.FromSeconds(1));
            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            Assert.Equal(2, server.Hits(ApiMetadata));
        }

        [Fact]
        public async Task Should_Subtract_The_Age_A_Shared_Cache_Reported() {
            FakeTimeProvider time = new();
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(Api), "max-age=60", age: TimeSpan.FromSeconds(50));
            OAuthDiscoveryClient client = Client(server, time: time);

            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            time.Advance(TimeSpan.FromSeconds(10));
            await client.GetProtectedResourceMetadataAsync(Api, Ct);

            Assert.Equal(2, server.Hits(ApiMetadata));
        }

        [Theory]
        [InlineData("no-store")]
        [InlineData("no-cache, max-age=600")]
        [InlineData("public")]
        [InlineData(null)]
        public async Task Should_Not_Cache_Without_Permission(string? cacheControl) {
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(Api), cacheControl);
            OAuthDiscoveryClient client = Client(server, time: new FakeTimeProvider());

            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            await client.GetProtectedResourceMetadataAsync(Api, Ct);

            Assert.Equal(2, server.Hits(ApiMetadata));
        }

        [Fact]
        public async Task Should_Cap_The_Lifetime_At_The_Configured_Maximum() {
            FakeTimeProvider time = new();
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(Api), "max-age=86400");
            OAuthDiscoveryClient client = Client(server, o => o.MaxCacheDuration = TimeSpan.FromMinutes(5), time);

            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            time.Advance(TimeSpan.FromMinutes(5));
            await client.GetProtectedResourceMetadataAsync(Api, Ct);

            Assert.Equal(2, server.Hits(ApiMetadata));
        }

        [Fact]
        public async Task Should_Not_Cache_A_Failure() {
            int calls = 0;
            FakeMetadataServer server = new FakeMetadataServer().Route(ApiMetadata, _ => ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(FakeMetadataServer.Resource(Api), Encoding.UTF8, "application/json") });
            OAuthDiscoveryClient client = Client(server, time: new FakeTimeProvider());

            await FailureAsync(() => client.GetProtectedResourceMetadataAsync(Api, Ct));
            ProtectedResourceMetadataDocument document = await client.GetProtectedResourceMetadataAsync(Api, Ct);

            Assert.Equal(Api, document.Resource);
        }

        [Fact]
        public async Task Should_Share_One_Fetch_Between_Concurrent_Requests() {
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            FakeMetadataServer server = new FakeMetadataServer().RouteAsync(ApiMetadata, async _ => {
                await release.Task;
                HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent(FakeMetadataServer.Resource(Api), Encoding.UTF8, "application/json") };
                response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true };
                return response;
            });
            OAuthDiscoveryClient client = Client(server, time: new FakeTimeProvider());

            Task<ProtectedResourceMetadataDocument>[] requests = [.. Enumerable.Range(0, 8).Select(_ => client.GetProtectedResourceMetadataAsync(Api, Ct))];
            await Task.Delay(50, Ct);
            release.SetResult();
            await Task.WhenAll(requests);

            Assert.Equal(1, server.Hits(ApiMetadata));
        }

        [Fact]
        public async Task Should_Let_One_Caller_Stop_Waiting_Without_Failing_The_Others() {
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            FakeMetadataServer server = new FakeMetadataServer().RouteAsync(ApiMetadata, async _ => {
                await release.Task;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(FakeMetadataServer.Resource(Api), Encoding.UTF8, "application/json") };
            });
            OAuthDiscoveryClient client = Client(server, time: new FakeTimeProvider());
            using CancellationTokenSource impatient = new();

            Task<ProtectedResourceMetadataDocument> first = client.GetProtectedResourceMetadataAsync(Api, impatient.Token);
            Task<ProtectedResourceMetadataDocument> second = client.GetProtectedResourceMetadataAsync(Api, Ct);
            await impatient.CancelAsync();
            release.SetResult();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.Equal(Api, (await second).Resource);
        }

        [Fact]
        public async Task Should_Keep_No_More_Documents_Than_The_Bound() {
            FakeMetadataServer server = new();
            for(int i = 0; i < 5; i++) {
                server.Json($"https://api{i}.example.com/.well-known/oauth-protected-resource", FakeMetadataServer.Resource($"https://api{i}.example.com"));
            }

            FakeTimeProvider time = new();
            OAuthDiscoveryOptions options = new() { MaxCachedDocuments = 2 };
            DiscoveryDocumentCache cache = new(options, time);
            OAuthDiscoveryClient client = new(new HttpClient(server), cache);

            for(int i = 0; i < 5; i++) {
                await client.GetProtectedResourceMetadataAsync($"https://api{i}.example.com", Ct);
                time.Advance(TimeSpan.FromSeconds(1));
            }

            Assert.Equal(2, cache.Count);

            // The most recent documents are the ones kept.
            await client.GetProtectedResourceMetadataAsync("https://api4.example.com", Ct);
            Assert.Equal(1, server.Hits("https://api4.example.com/.well-known/oauth-protected-resource"));
            await client.GetProtectedResourceMetadataAsync("https://api0.example.com", Ct);
            Assert.Equal(2, server.Hits("https://api0.example.com/.well-known/oauth-protected-resource"));
        }

        [Fact]
        public async Task Should_Not_Let_Failed_Fetches_Occupy_The_Cache() {
            // Eviction only considers documents; a failed entry left behind would never be evicted and would grow the cache
            // past its bound with every unreachable URL.
            FakeMetadataServer server = new();
            DiscoveryDocumentCache cache = new(new OAuthDiscoveryOptions { MaxCachedDocuments = 2 }, new FakeTimeProvider());
            OAuthDiscoveryClient client = new(new HttpClient(server), cache);

            for(int i = 0; i < 5; i++) {
                await FailureAsync(() => client.GetProtectedResourceMetadataAsync($"https://gone{i}.example.com", Ct));
            }

            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void Should_Refuse_Options_That_Cannot_Work() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OAuthDiscoveryClient(new HttpClient(), new OAuthDiscoveryOptions { MaxCachedDocuments = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OAuthDiscoveryClient(new HttpClient(), new OAuthDiscoveryOptions { MaxCacheDuration = TimeSpan.FromSeconds(-1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OAuthDiscoveryClient(new HttpClient(), new OAuthDiscoveryOptions { MaxDocumentBytes = 0 }));
        }
    }

    public sealed class FromAChallenge {
        private static FakeMetadataServer Server(string resource = Api) {
            return new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(resource, Vaultex), "max-age=3600");
        }

        private static HttpResponseMessage Challenged(string requestUrl) {
            return FakeMetadataServer.Challenge(requestUrl, $"Bearer error=\"invalid_token\", resource_metadata=\"{ApiMetadata}\"");
        }

        [Fact]
        public async Task Should_Accept_A_Resource_Identical_To_The_Requested_Url() {
            ProtectedResourceMetadataDocument? document = await Client(Server()).GetProtectedResourceMetadataAsync(Challenged(Api), Ct);

            Assert.Equal(Api, document?.Resource);
        }

        [Theory]
        [InlineData("https://api.example.com/v1/users")]
        [InlineData("https://api.example.com/v1/")]
        [InlineData("https://api.example.com/v1?x=1")]
        [InlineData("https://api.example.com/V1")]
        [InlineData("https://api.example.com/v10")]
        [InlineData("https://other.example.com/v1")]
        [InlineData("http://api.example.com/v1")]
        public async Task Should_Refuse_A_Resource_That_Is_Not_The_Requested_Url(string requestUrl) {
            // RFC 9728 §3.3: identical, not merely covering the request.
            Assert.Equal(OAuthDiscoveryFailure.IdentifierMismatch, await FailureAsync(() => Client(Server()).GetProtectedResourceMetadataAsync(Challenged(requestUrl), Ct)));
        }

        [Fact]
        public async Task Should_Accept_A_Root_Resource_Requested_Without_Its_Slash() {
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource("https://api.example.com", Vaultex));

            ProtectedResourceMetadataDocument? document = await Client(server).GetProtectedResourceMetadataAsync(Challenged("https://api.example.com"), Ct);

            Assert.Equal("https://api.example.com", document?.Resource);
        }
        [Fact]
        public async Task Should_Return_Null_When_The_Response_Advertises_No_Metadata() {
            HttpResponseMessage response = FakeMetadataServer.Challenge(Api, "Bearer error=\"invalid_token\"");

            Assert.Null(await Client(Server()).GetProtectedResourceMetadataAsync(response, Ct));
            Assert.Null(await Client(Server()).DiscoverAsync(response, Ct));
        }

        [Fact]
        public async Task Should_Require_The_Request_Url() {
            HttpResponseMessage response = new(HttpStatusCode.Unauthorized);
            response.Headers.TryAddWithoutValidation("WWW-Authenticate", $"Bearer resource_metadata=\"{ApiMetadata}\"");

            await Assert.ThrowsAsync<ArgumentException>(() => Client(Server()).GetProtectedResourceMetadataAsync(response, Ct));
        }

        [Theory]
        [InlineData("/.well-known/oauth-protected-resource", OAuthDiscoveryFailure.InvalidUrl)]
        [InlineData("http://api.example.com/.well-known/oauth-protected-resource/v1", OAuthDiscoveryFailure.InsecureTransport)]
        public async Task Should_Refuse_An_Advertised_Url_It_Cannot_Fetch_Securely(string advertised, OAuthDiscoveryFailure failure) {
            FakeMetadataServer server = Server();
            HttpResponseMessage response = FakeMetadataServer.Challenge(Api, $"Bearer resource_metadata=\"{advertised}\"");

            Assert.Equal(failure, await FailureAsync(() => Client(server).GetProtectedResourceMetadataAsync(response, Ct)));
            Assert.Equal(0, server.TotalHits);
        }

        [Fact]
        public async Task Should_Refetch_A_Fresh_Document_On_A_Challenge_Once_The_Refresh_Interval_Has_Passed() {
            // §5.2: a new challenge signals the metadata may have changed; a burst of them must not become a burst of fetches.
            FakeTimeProvider time = new();
            FakeMetadataServer server = Server();
            OAuthDiscoveryClient client = Client(server, o => o.ChallengeRefreshInterval = TimeSpan.FromSeconds(30), time);

            await client.GetProtectedResourceMetadataAsync(Challenged(Api), Ct);
            time.Advance(TimeSpan.FromSeconds(29));
            await client.GetProtectedResourceMetadataAsync(Challenged(Api), Ct);
            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            Assert.Equal(1, server.Hits(ApiMetadata));

            time.Advance(TimeSpan.FromSeconds(1));
            await client.GetProtectedResourceMetadataAsync(Challenged(Api), Ct);
            Assert.Equal(2, server.Hits(ApiMetadata));

            // A lookup by identifier does not refresh a fresh document.
            time.Advance(TimeSpan.FromMinutes(5));
            await client.GetProtectedResourceMetadataAsync(Api, Ct);
            Assert.Equal(2, server.Hits(ApiMetadata));
        }
    }

    public sealed class AuthorizationServerMetadata {
        [Theory]
        [InlineData("https://vaultex.example.com", "https://vaultex.example.com/.well-known/oauth-authorization-server")]
        [InlineData("https://vaultex.example.com/tenant1/", "https://vaultex.example.com/.well-known/oauth-authorization-server/tenant1")]
        public async Task Should_Fetch_From_The_Rfc_8414_Derived_Url(string issuer, string url) {
            FakeMetadataServer server = new FakeMetadataServer().Json(url, FakeMetadataServer.Issuer(issuer));

            AuthorizationServerMetadataDocument document = await Client(server).GetAuthorizationServerMetadataAsync(issuer, Ct);

            Assert.Equal(issuer, document.Issuer);
            Assert.Equal($"{issuer.TrimEnd('/')}/device", document.DeviceAuthorizationEndpoint);
            Assert.Equal(["authorization_code", "implicit"], document.GrantTypesSupported);
            Assert.Equal(["client_secret_basic"], document.TokenEndpointAuthMethodsSupported);
            Assert.Null(document.ProtectedResources);
        }

        [Theory]
        [InlineData("https://vaultex.example.com/")]
        [InlineData("https://attacker.example.com")]
        public async Task Should_Refuse_A_Document_For_Another_Issuer(string claimed) {
            // RFC 8414 §6.2: a document naming another issuer is an impersonation attempt.
            FakeMetadataServer server = new FakeMetadataServer().Json(VaultexMetadata, FakeMetadataServer.Issuer(claimed));

            Assert.Equal(OAuthDiscoveryFailure.IdentifierMismatch, await FailureAsync(() => Client(server).GetAuthorizationServerMetadataAsync(Vaultex, Ct)));
        }

        [Fact]
        public async Task Should_Require_The_Issuer_Parameter() {
            FakeMetadataServer server = new FakeMetadataServer().Json(VaultexMetadata, """{"token_endpoint":"https://vaultex.example.com/token"}""");

            Assert.Equal(OAuthDiscoveryFailure.InvalidDocument, await FailureAsync(() => Client(server).GetAuthorizationServerMetadataAsync(Vaultex, Ct)));
        }
    }

    public sealed class Discover {
        [Fact]
        public async Task Should_Go_From_A_Challenge_To_The_Authorization_Servers_Endpoints() {
            FakeMetadataServer server = new FakeMetadataServer()
                .Json(ApiMetadata, FakeMetadataServer.Resource(Api, Vaultex))
                .Json(VaultexMetadata, FakeMetadataServer.Issuer(Vaultex));

            OAuthDiscoveryResult? result = await Client(server).DiscoverAsync(
                FakeMetadataServer.Challenge(Api, $"Bearer resource_metadata=\"{ApiMetadata}\""), Ct);

            Assert.NotNull(result);
            Assert.Equal(Api, result.ProtectedResource.Resource);
            Assert.Equal("https://vaultex.example.com/token", result.AuthorizationServer.TokenEndpoint);
        }

        [Fact]
        public async Task Should_Use_The_First_Listed_Server_When_None_Are_Configured_As_Trusted() {
            FakeMetadataServer server = new FakeMetadataServer()
                .Json(ApiMetadata, FakeMetadataServer.Resource(Api, Vaultex, "https://other.example.com"))
                .Json(VaultexMetadata, FakeMetadataServer.Issuer(Vaultex));

            OAuthDiscoveryResult result = await Client(server).DiscoverAsync(Api, Ct);

            Assert.Equal(Vaultex, result.AuthorizationServer.Issuer);
        }

        [Fact]
        public async Task Should_Use_The_First_Trusted_Server_And_Never_Fetch_An_Untrusted_One() {
            FakeMetadataServer server = new FakeMetadataServer()
                .Json(ApiMetadata, FakeMetadataServer.Resource(Api, "https://attacker.example.com", Vaultex))
                .Json(VaultexMetadata, FakeMetadataServer.Issuer(Vaultex));

            OAuthDiscoveryResult result = await Client(server, o => o.TrustedAuthorizationServers.Add(Vaultex)).DiscoverAsync(Api, Ct);

            Assert.Equal(Vaultex, result.AuthorizationServer.Issuer);
            Assert.Equal(0, server.Hits("https://attacker.example.com/.well-known/oauth-authorization-server"));
        }

        [Fact]
        public async Task Should_Refuse_A_Resource_Listing_Only_Untrusted_Servers() {
            FakeMetadataServer server = new FakeMetadataServer()
                .Json(ApiMetadata, FakeMetadataServer.Resource(Api, "https://attacker.example.com"));

            OAuthDiscoveryClient client = Client(server, o => o.TrustedAuthorizationServers.Add(Vaultex));

            Assert.Equal(OAuthDiscoveryFailure.UntrustedAuthorizationServer, await FailureAsync(() => client.DiscoverAsync(Api, Ct)));
            Assert.Equal(1, server.TotalHits);
        }

        [Fact]
        public async Task Should_Refuse_A_Resource_Listing_No_Servers() {
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(Api));

            Assert.Equal(OAuthDiscoveryFailure.NoAuthorizationServer, await FailureAsync(() => Client(server).DiscoverAsync(Api, Ct)));
        }
    }

    public sealed class Registration {
        [Fact]
        public async Task Should_Share_The_Cache_Between_Typed_Client_Instances() {
            FakeMetadataServer server = new FakeMetadataServer().Json(ApiMetadata, FakeMetadataServer.Resource(Api), "max-age=600");
            ServiceCollection services = new();
            services.AddOAuthDiscoveryClient(o => o.MaxRedirects = 1).ConfigurePrimaryHttpMessageHandler(() => server);

            await using ServiceProvider provider = services.BuildServiceProvider();
            OAuthDiscoveryClient first = provider.GetRequiredService<OAuthDiscoveryClient>();
            OAuthDiscoveryClient second = provider.GetRequiredService<OAuthDiscoveryClient>();

            Assert.NotSame(first, second);
            await first.GetProtectedResourceMetadataAsync(Api, Ct);
            await second.GetProtectedResourceMetadataAsync(Api, Ct);
            Assert.Equal(1, server.Hits(ApiMetadata));
        }

        [Fact]
        public void Should_Not_Follow_Redirects_In_The_Default_Handler() {
            ServiceCollection services = new();
            services.AddOAuthDiscoveryClient();

            using ServiceProvider provider = services.BuildServiceProvider();
            Microsoft.Extensions.Http.HttpMessageHandlerBuilder builder = provider.GetRequiredService<Microsoft.Extensions.Http.HttpMessageHandlerBuilder>();
            builder.Name = nameof(OAuthDiscoveryClient);
            foreach(Action<Microsoft.Extensions.Http.HttpMessageHandlerBuilder> configure in provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.Extensions.Http.HttpClientFactoryOptions>>()
                .Get(nameof(OAuthDiscoveryClient)).HttpMessageHandlerBuilderActions) {
                configure(builder);
            }

            SocketsHttpHandler handler = Assert.IsType<SocketsHttpHandler>(builder.PrimaryHandler);
            Assert.False(handler.AllowAutoRedirect);
        }
    }
}
