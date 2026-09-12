using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Integration;

/// <summary>
/// The application's JSON naming policy reaching the query string, and a custom filter declared on the schema
/// reaching a handler — through the real binder, validation filter and engine.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Querying")]
[Trait("Component", "Configuration")]
public sealed class NamingPolicyAndCustomFilterEndToEndTests : IAsyncLifetime {

    public sealed class Key {
        public int Id { get; set; }
        public string ContentType { get; set; } = "";
        public int ScreenshotCount { get; set; }
    }

    private static readonly Key[] Keys = [
        new() { Id = 1, ContentType = "text", ScreenshotCount = 0 },
        new() { Id = 2, ContentType = "html", ScreenshotCount = 3 },
        new() { Id = 3, ContentType = "text", ScreenshotCount = 1 }
    ];

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();

        // The application writes snake_case bodies; the query string should follow, via UseJsonNamingPolicy.
        builder.Services.Configure<JsonOptions>(json => json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        builder.Services.AddQuerying(querying => querying
            .UseJsonNamingPolicy()
            .AddSchema<Key>(schema => {
                schema.Property(k => k.ContentType).AllowFilter(QueryOperator.Equal).AllowSort();
                schema.CustomFilter<bool>("hasScreenshot").AllowFilter(QueryOperator.Equal);
            }));

        this._app = builder.Build();

        this._app.MapGet("/keys", (Query<Key> query, QuerySchema<Key> schema) => {
            IEnumerable<Key> keys = Keys.AsQueryable().ApplyQuery(query.Value, schema);

            // The custom filter is the endpoint's to apply, with a typed value the schema already validated.
            if(schema.TryGetFilterValue(query.Value, "hasScreenshot", out bool hasScreenshot)) {
                keys = keys.Where(k => (k.ScreenshotCount > 0) == hasScreenshot);
            }

            return Results.Ok(keys.Select(k => k.Id).OrderBy(id => id).ToArray());
        }).WithQueryValidation<Key>();

        await this._app.StartAsync();
        this._client = this._app.GetTestClient();
    }

    public async ValueTask DisposeAsync() {
        await this._app.DisposeAsync();
    }

    private async Task<(HttpStatusCode Status, int[]? Ids)> GetAsync(string query) {
        HttpResponseMessage response = await this._client.GetAsync($"/keys?{query}", TestContext.Current.CancellationToken);

        return response.StatusCode == HttpStatusCode.OK
            ? (response.StatusCode, await response.Content.ReadFromJsonAsync<int[]>(TestContext.Current.CancellationToken))
            : (response.StatusCode, null);
    }

    [Fact]
    public async Task The_Policy_Name_Should_Filter() {
        (HttpStatusCode status, int[]? ids) = await GetAsync("content_type=text");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([1, 3], ids);
    }

    [Fact]
    public async Task The_Original_Name_Should_Still_Filter() {
        (HttpStatusCode status, int[]? ids) = await GetAsync("ContentType=text");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([1, 3], ids);
    }

    [Fact]
    public async Task The_Policy_Name_Should_Sort() {
        (HttpStatusCode status, _) = await GetAsync("sort=-content_type");

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task A_Custom_Filter_Should_Reach_The_Handler_Typed() {
        (HttpStatusCode status, int[]? ids) = await GetAsync("hasScreenshot=true&content_type=text");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([3], ids);
    }

    [Fact]
    public async Task A_Custom_Filter_Should_Be_Validated() {
        (HttpStatusCode status, _) = await GetAsync("hasScreenshot=sometimes");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}
