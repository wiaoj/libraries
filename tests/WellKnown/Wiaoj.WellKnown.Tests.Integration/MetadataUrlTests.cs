using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

namespace Wiaoj.WellKnown.Tests.Integration;

/// <summary>
/// The metadata URL is derived from the resource identifier (RFC 9728 §3), and several resources share a host (#102).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "WellKnown")]
[Trait("Component", "MetadataUrl")]
public sealed class MetadataUrlTests {

    public sealed class Derivation {
        [Theory]
        [InlineData("https://api.example.com", "/.well-known/oauth-protected-resource")]
        [InlineData("https://api.example.com/", "/.well-known/oauth-protected-resource")]
        [InlineData("https://api.example.com/resource1", "/.well-known/oauth-protected-resource/resource1")]
        [InlineData("https://api.example.com/v1/", "/.well-known/oauth-protected-resource/v1/")]
        [InlineData("https://api.example.com/tenants/a/api", "/.well-known/oauth-protected-resource/tenants/a/api")]
        public void Should_Insert_The_Suffix_Between_Host_And_Path(string resource, string path) {
            Assert.Equal(path, ProtectedResourceMetadataUri.PathFor(resource));
        }

        [Fact]
        public void Should_Keep_The_Identifiers_Scheme_And_Authority() {
            Assert.Equal(
                "https://api.example.com:8443/.well-known/oauth-protected-resource/v1",
                ProtectedResourceMetadataUri.For("https://api.example.com:8443/v1").AbsoluteUri);
        }

        [Theory]
        [InlineData("https://api.example.com/v1?tenant=a")]
        [InlineData("https://api.example.com/v1#top")]
        [InlineData("api.example.com/v1")]
        public void Should_Refuse_An_Identifier_It_Cannot_Route(string resource) {
            Assert.ThrowsAny<ArgumentException>(() => ProtectedResourceMetadataUri.PathFor(resource));
        }
    }

    public sealed class Serving {
        [Fact]
        public async Task Should_Serve_A_Resource_With_A_Path_At_Its_Derived_Path() {
            await using TestApp app = await TestApp.StartAsync(
                s => s.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com/v1"),
                a => a.MapOAuthProtectedResource());

            HttpResponseMessage derived = await app.Client.GetAsync("/.well-known/oauth-protected-resource/v1", TestContext.Current.CancellationToken);
            HttpResponseMessage root = await app.Client.GetAsync("/.well-known/oauth-protected-resource", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, derived.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
        }

        [Fact]
        public async Task Should_Serve_Each_Named_Resource_Its_Own_Document() {
            await using TestApp app = await TestApp.StartAsync(
                s => {
                    s.AddOAuthProtectedResource("public", r => r.Resource = "https://api.example.com/v1");
                    s.AddOAuthProtectedResource("admin", r => r.Resource = "https://api.example.com/admin");
                    s.AddProtectedResourceScopes("admin", ["users:manage"]);
                },
                a => a.MapOAuthProtectedResource());

            JsonElement pub = JsonDocument.Parse(await app.Client.GetStringAsync("/.well-known/oauth-protected-resource/v1", TestContext.Current.CancellationToken)).RootElement;
            JsonElement admin = JsonDocument.Parse(await app.Client.GetStringAsync("/.well-known/oauth-protected-resource/admin", TestContext.Current.CancellationToken)).RootElement;

            Assert.Equal("https://api.example.com/v1", pub.GetProperty("resource").GetString());
            Assert.False(pub.TryGetProperty("scopes_supported", out _));
            Assert.Equal("https://api.example.com/admin", admin.GetProperty("resource").GetString());
            Assert.Equal("users:manage", admin.GetProperty("scopes_supported")[0].GetString());
        }

        [Fact]
        public void Should_Refuse_Two_Resources_With_The_Same_Identifier() {
            WebApplication app = TestApp.Build(s => {
                s.AddOAuthProtectedResource("a", r => r.Resource = "https://api.example.com/v1");
                s.AddOAuthProtectedResource("b", r => r.Resource = "https://api.example.com/v1/");
            }, _ => { });

            // "/v1" and "/v1/" are different identifiers with different paths; the same identifier twice is not.
            app.MapOAuthProtectedResource();

            WebApplication duplicate = TestApp.Build(s => {
                s.AddOAuthProtectedResource("a", r => r.Resource = "https://api.example.com/v1");
                s.AddOAuthProtectedResource("b", r => r.Resource = "https://API.example.com/v1");
            }, _ => { });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => duplicate.MapOAuthProtectedResource());
            Assert.Contains("'a' and 'b'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Still_Answer_At_The_Origin_Root_When_The_App_Uses_A_Path_Base() {
            // UsePathBase("/api") moves a matching prefix into PathBase and leaves other requests alone, so the
            // document stays reachable at the origin's /.well-known — where RFC 9728 clients look — and under the base.
            await using TestApp app = await TestApp.StartAsync(
                s => s.AddOAuthProtectedResource(r => r.Resource = "https://api.example.com/api"),
                a => {
                    a.UsePathBase("/api");
                    a.UseRouting();
                    a.MapOAuthProtectedResource();
                });

            HttpResponseMessage root = await app.Client.GetAsync("/.well-known/oauth-protected-resource/api", TestContext.Current.CancellationToken);
            HttpResponseMessage underBase = await app.Client.GetAsync("/api/.well-known/oauth-protected-resource/api", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, root.StatusCode);
            Assert.Equal(HttpStatusCode.OK, underBase.StatusCode);
        }
        [Fact]
        public void Should_Refuse_To_Map_When_No_Resource_Is_Registered() {
            WebApplication app = TestApp.Build(_ => { }, _ => { });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => app.MapOAuthProtectedResource());
            Assert.Contains("AddOAuthProtectedResource", error.Message, StringComparison.Ordinal);
        }
    }
}
