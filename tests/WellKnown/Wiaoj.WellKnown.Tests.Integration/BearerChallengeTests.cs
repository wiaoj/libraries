using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wiaoj.WellKnown.Tests.Integration;

/// <summary>
/// <c>resource_metadata</c> is added to JwtBearer's own challenge, beside its error parameters (RFC 9728 §5.1, #103).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "WellKnown")]
[Trait("Component", "Challenge")]
public sealed class BearerChallengeTests {
    private const string Resource = "https://api.example.com/v1";
    private const string MetadataUrl = "https://api.example.com/.well-known/oauth-protected-resource/v1";

    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef0123456789abcdef"));

    private static string Token(DateTime expires) {
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor {
            Expires = expires,
            NotBefore = expires.AddHours(-2),
            IssuedAt = expires.AddHours(-2),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        });
    }

    private static void ConfigureBearer(JwtBearerOptions options) {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = Key,
            ClockSkew = TimeSpan.Zero
        };
    }

    private static Task<TestApp> StartAsync(Action<Microsoft.AspNetCore.Authentication.AuthenticationBuilder> authentication, Action<IServiceCollection>? services = null) {
        return TestApp.StartAsync(
            s => {
                s.AddOAuthProtectedResource(r => r.Resource = Resource);
                authentication(s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme));
                s.AddAuthorization();
                services?.Invoke(s);
            },
            a => {
                a.UseAuthentication();
                a.UseAuthorization();
                a.MapOAuthProtectedResource();
                a.MapGet("/v1/assets", () => "ok").RequireAuthorization();
            });
    }

    private static async Task<HttpResponseMessage> GetAsync(TestApp app, string? token = null) {
        using HttpRequestMessage request = new(HttpMethod.Get, "/v1/assets");
        if(token is not null) {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await app.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string Challenge(HttpResponseMessage response) {
        return Assert.Single(response.Headers.WwwAuthenticate).ToString();
    }

    [Fact]
    public async Task Should_Advertise_The_Metadata_When_No_Token_Is_Sent() {
        await using TestApp app = await StartAsync(a => a.AddJwtBearer(ConfigureBearer).AddProtectedResourceMetadataChallenge());

        HttpResponseMessage response = await GetAsync(app);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal($"Bearer resource_metadata=\"{MetadataUrl}\"", Challenge(response));
    }

    [Fact]
    public async Task Should_Keep_The_Error_Of_An_Expired_Token_In_The_Same_Challenge() {
        // The previous helper replaced the header, and a client could no longer tell expired from missing.
        await using TestApp app = await StartAsync(a => a.AddJwtBearer(ConfigureBearer).AddProtectedResourceMetadataChallenge());

        string challenge = Challenge(await GetAsync(app, Token(DateTime.UtcNow.AddHours(-1))));

        Assert.StartsWith("Bearer error=\"invalid_token\"", challenge, StringComparison.Ordinal);
        Assert.Contains("error_description=\"The token expired at", challenge, StringComparison.Ordinal);
        Assert.EndsWith($", resource_metadata=\"{MetadataUrl}\"", challenge, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Leave_An_Authorized_Request_Untouched() {
        await using TestApp app = await StartAsync(a => a.AddJwtBearer(ConfigureBearer).AddProtectedResourceMetadataChallenge());

        HttpResponseMessage response = await GetAsync(app, Token(DateTime.UtcNow.AddHours(1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task Should_Run_An_OnChallenge_Handler_The_Application_Set() {
        await using TestApp app = await StartAsync(a => a
            .AddJwtBearer(options => {
                ConfigureBearer(options);
                options.Events = new JwtBearerEvents {
                    OnChallenge = context => {
                        context.Response.Headers["X-Challenged"] = "yes";
                        return Task.CompletedTask;
                    }
                };
            })
            .AddProtectedResourceMetadataChallenge());

        HttpResponseMessage response = await GetAsync(app);

        Assert.Equal("yes", Assert.Single(response.Headers.GetValues("X-Challenged")));
        Assert.Contains("resource_metadata=", Challenge(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Leave_A_Challenge_The_Application_Handled_Itself() {
        await using TestApp app = await StartAsync(a => a
            .AddJwtBearer(options => {
                ConfigureBearer(options);
                options.Events = new JwtBearerEvents {
                    OnChallenge = async context => {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer realm=\"custom\"";
                        await context.Response.WriteAsync("custom", TestContext.Current.CancellationToken);
                    }
                };
            })
            .AddProtectedResourceMetadataChallenge());

        HttpResponseMessage response = await GetAsync(app);

        Assert.Equal("Bearer realm=\"custom\"", Challenge(response));
    }

    [Fact]
    public async Task The_Advertised_Url_Should_Serve_A_Document_For_The_Same_Resource() {
        // What a client does next (§3.3): fetch the URL and require resource to be identical.
        await using TestApp app = await StartAsync(a => a.AddJwtBearer(ConfigureBearer).AddProtectedResourceMetadataChallenge());

        string challenge = Challenge(await GetAsync(app));
        Uri advertised = new(challenge[(challenge.IndexOf('"') + 1)..challenge.LastIndexOf('"')]);
        JsonElement document = JsonDocument.Parse(await app.Client.GetStringAsync(advertised.PathAndQuery, TestContext.Current.CancellationToken)).RootElement;

        Assert.Equal(Resource, document.GetProperty("resource").GetString());
    }

    [Fact]
    public async Task Should_Advertise_The_Named_Resource_Chosen_For_The_Scheme() {
        await using TestApp app = await StartAsync(
            a => a.AddJwtBearer(ConfigureBearer).AddProtectedResourceMetadataChallenge(resourceName: "admin"),
            s => s.AddOAuthProtectedResource("admin", r => r.Resource = "https://api.example.com/admin"));

        Assert.Equal("Bearer resource_metadata=\"https://api.example.com/.well-known/oauth-protected-resource/admin\"", Challenge(await GetAsync(app)));
    }

    [Fact]
    public async Task Should_Refuse_Events_Supplied_Through_EventsType() {
        await using TestApp app = await StartAsync(
            a => a.AddJwtBearer(options => {
                ConfigureBearer(options);
                options.EventsType = typeof(JwtBearerEvents);
            }).AddProtectedResourceMetadataChallenge(),
            s => s.AddSingleton<JwtBearerEvents>());

        // Development answers the exception with a 500 error page; the point is that it fails rather than advertising nothing.
        HttpResponseMessage response = await GetAsync(app);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("resolves its events from EventsType", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Leave_A_Response_That_Did_Not_End_As_A_401() {
        // An insufficient_scope 403 names the scope to request; the metadata parameter belongs to 401 challenges.
        await using TestApp app = await TestApp.StartAsync(
            s => s.AddOAuthProtectedResource(r => r.Resource = Resource),
            a => a.MapGet("/v1/assets", (HttpContext context) => {
                ProtectedResourceChallenge.AddOnStarting(context, new Uri(MetadataUrl));
                context.Response.Headers.WWWAuthenticate = "Bearer error=\"insufficient_scope\", scope=\"assets:write\"";
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }));

        HttpResponseMessage response = await app.Client.GetAsync("/v1/assets", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Bearer error=\"insufficient_scope\", scope=\"assets:write\"", Challenge(response));
    }

    [Fact]
    public async Task The_Obsolete_Helper_Should_No_Longer_Drop_The_Error() {
        await using TestApp app = await StartAsync(a => a.AddJwtBearer(options => {
            ConfigureBearer(options);
            options.Events = new JwtBearerEvents {
#pragma warning disable CS0618 // The helper under test is obsolete on purpose.
                OnChallenge = context => {
                    context.AttachProtectedResourceMetadata(MetadataUrl);
                    return Task.CompletedTask;
                }
#pragma warning restore CS0618
            };
        }));

        string challenge = Challenge(await GetAsync(app, Token(DateTime.UtcNow.AddHours(-1))));

        Assert.StartsWith("Bearer error=\"invalid_token\"", challenge, StringComparison.Ordinal);
        Assert.EndsWith($", resource_metadata=\"{MetadataUrl}\"", challenge, StringComparison.Ordinal);
    }

    public sealed class AddingTheParameter {
        private static readonly Uri Url = new(MetadataUrl);

        [Fact]
        public void Should_Leave_Other_Schemes_Unchanged() {
            StringValues result = ProtectedResourceChallenge.AddParameter(new StringValues(["Basic realm=\"x\"", "Bearer error=\"invalid_token\""]), Url);

            Assert.Equal(["Basic realm=\"x\"", $"Bearer error=\"invalid_token\", resource_metadata=\"{MetadataUrl}\""], result.ToArray());
        }

        [Fact]
        public void Should_Not_Add_It_Twice() {
            string existing = $"Bearer resource_metadata=\"{MetadataUrl}\"";

            Assert.Equal([existing], ProtectedResourceChallenge.AddParameter(existing, Url).ToArray());
        }

        [Fact]
        public void Should_Add_A_Bearer_Challenge_When_There_Is_None() {
            Assert.Equal(["Basic realm=\"x\"", $"Bearer resource_metadata=\"{MetadataUrl}\""],
                ProtectedResourceChallenge.AddParameter("Basic realm=\"x\"", Url).ToArray());
        }

        [Fact]
        public void Should_Not_Mistake_A_Scheme_That_Only_Starts_With_Bearer() {
            Assert.Equal(["BearerX a=\"b\"", $"Bearer resource_metadata=\"{MetadataUrl}\""],
                ProtectedResourceChallenge.AddParameter("BearerX a=\"b\"", Url).ToArray());
        }

        [Theory]
        [InlineData("https://api.example.com/a\"b", "https://api.example.com/a%22b")]
        [InlineData("https://api.example.com/a\\b", "https://api.example.com/a/b")]
        public void Should_Not_Let_The_Url_Break_Out_Of_Its_Quoted_String(string url, string written) {
            StringValues result = ProtectedResourceChallenge.AddParameter("Bearer", new Uri(url));

            Assert.Equal($"Bearer resource_metadata=\"{written}\"", result.ToString());
        }
    }
}
