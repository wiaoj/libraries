using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.AspNetCore.Binders;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Integration.Endpoints;

/// <summary>
/// Accept-Query advertises QUERY support only where QUERY is accepted, with every registered parser's media type, and a
/// refused media type is answered with Accept (RFC 10008 §3, Appendix A.3; #144).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Querying")]
[Trait("Component", "AcceptQuery")]
public sealed class AcceptQueryHeaderTests : IAsyncLifetime {
    private const string CustomMediaType = "application/vnd.acme.query";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public sealed class Item {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>A custom parser the application registered; its media type must be advertised too.</summary>
    private sealed class AcmeParser : IQueryPayloadParser {
        public IReadOnlyList<string> SupportedMediaTypes { get; } = [CustomMediaType];

        public bool CanParse(string mediaType) => mediaType.StartsWith(CustomMediaType, StringComparison.OrdinalIgnoreCase);

        public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
            result = QueryRequest.Empty;
            return true;
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            // As the other fixtures: the developer exception page turns the binder's BadHttpRequestException into its status
            // code, as Kestrel does for a real host; TestServer alone would surface the exception to the client.
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddQuerying()
            .AddSchema<Item>(schema => schema.AllowFilter(x => x.Name))
            .AddPayloadParser<AcmeParser>();

        this._app = builder.Build();

        Item[] items = [new() { Id = 1, Name = "a" }];
        IResult Handle(Query<Item> query, QuerySchema<Item> schema) => Results.Ok(items.AsQueryable().ApplyQuery(query, schema).ToList());

        this._app.MapMethods("/items", ["GET", "QUERY", "POST"], Handle).WithQueryValidation<Item>();
        this._app.MapGet("/items/get-only", Handle).WithQueryValidation<Item>();
        this._app.MapPost("/items/post-only", Handle).WithQueryValidation<Item>();

        await this._app.StartAsync(Ct);
        this._client = this._app.GetTestClient();
    }

    public async ValueTask DisposeAsync() {
        await this._app.DisposeAsync();
    }

    private static string? Header(HttpResponseMessage response, string name) {
        return response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(", ", values)
            : response.Content.Headers.TryGetValues(name, out values) ? string.Join(", ", values)
            : null;
    }

    private static HttpRequestMessage Body(string method, string path, string mediaType, string body = "x") {
        HttpRequestMessage request = new(new HttpMethod(method), path) { Content = new StringContent(body, Encoding.UTF8) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return request;
    }

    [Fact]
    public async Task Should_Not_Advertise_Query_Support_On_A_GET_Only_Endpoint() {
        // It used to: the validation filter wrote Accept-Query on every validated request.
        using HttpResponseMessage response = await this._client.GetAsync("/items/get-only?name=a", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Header(response, "Accept-Query"));
    }

    [Fact]
    public async Task Should_Not_Advertise_Query_Support_On_A_POST_Only_Endpoint() {
        using HttpResponseMessage response = await this._client.SendAsync(Body("POST", "/items/post-only", "application/json", """{"q":"a"}"""), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Header(response, "Accept-Query"));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("QUERY")]
    public async Task Should_Advertise_Every_Registered_Media_Type_On_Any_Response_Of_A_QUERY_Endpoint(string method) {
        // RFC 10008 §3: the value applies to the whole path, so every method must advertise the same, complete list.
        using HttpRequestMessage request = method == "GET"
            ? new HttpRequestMessage(HttpMethod.Get, "/items?name=a")
            : Body(method, "/items", "application/json", """{"q":"a"}""");

        using HttpResponseMessage response = await this._client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"application/json, text/plain, application/x-www-form-urlencoded, {CustomMediaType}", Header(response, "Accept-Query"));
    }

    [Fact]
    public async Task Should_Answer_An_Unsupported_Media_Type_With_Accept_On_A_QUERY_Endpoint() {
        using HttpResponseMessage response = await this._client.SendAsync(Body("QUERY", "/items", "application/xml", "<q/>"), Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        string expected = $"application/json, text/plain, application/x-www-form-urlencoded, {CustomMediaType}";
        Assert.Equal(expected, Header(response, "Accept"));
        Assert.Equal(expected, Header(response, "Accept-Query"));
    }

    [Fact]
    public async Task Should_Answer_An_Unsupported_Media_Type_With_Accept_On_A_POST_Only_Endpoint() {
        using HttpResponseMessage response = await this._client.SendAsync(Body("POST", "/items/post-only", "application/xml", "<q/>"), Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.NotNull(Header(response, "Accept"));
        Assert.Null(Header(response, "Accept-Query"));
    }

    [Fact]
    public async Task Should_Not_Add_Accept_To_A_Successful_Response() {
        using HttpResponseMessage response = await this._client.SendAsync(Body("QUERY", "/items", "application/json", """{"q":"a"}"""), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Header(response, "Accept"));
    }

    [Fact]
    public void Should_Write_A_Media_Type_As_A_String_When_It_Is_Not_A_Structured_Field_Token() {
        // RFC 10008 §3: "Media types do not exactly map to Tokens; for instance, they allow a leading digit."
        Assert.Equal(
            "application/json, \"3gpp-ims/query\", \"a b/c\"",
            QueryPayloadMediaTypes.FormatStructuredList(["application/json", "3gpp-ims/query", "a b/c"]));
    }

    [Fact]
    public void Should_Leave_Out_Parameters_And_Duplicates() {
        ServiceCollection services = new();
        services.AddSingleton<IQueryPayloadParser, JsonQueryPayloadParser>();
        services.AddSingleton<IQueryPayloadParser>(new ParameterisedParser());

        Assert.Equal(["application/json"], QueryPayloadMediaTypes.Resolve(services.BuildServiceProvider()));
    }

    private sealed class ParameterisedParser : IQueryPayloadParser {
        public IReadOnlyList<string> SupportedMediaTypes { get; } = ["APPLICATION/JSON; charset=utf-8"];
        public bool CanParse(string mediaType) => false;
        public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
            result = QueryRequest.Empty;
            return false;
        }
    }
}
