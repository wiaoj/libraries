using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Integration;

/// <summary>
/// Two endpoints over one entity, each with its own schema (#95): the validation filter and the binder both apply the
/// schema the endpoint selected, and an endpoint that selects none refuses to guess.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Querying")]
[Trait("Component", "SchemaContract")]
public sealed class SchemaPerEndpointEndToEndTests {

    public sealed class Asset {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public int OwnerId { get; set; }
    }

    public sealed class AdminAssetSchema : QuerySchema<Asset> {
        public AdminAssetSchema() {
            AllowFilter(a => a.FileName, QueryOperator.Equal);
            AllowFilter(a => a.OwnerId, QueryOperator.Equal);

            // Only this contract tolerates a tracing parameter the binder must skip.
            IgnoreParameters("trace");
        }
    }

    public sealed class PublicAssetSchema : QuerySchema<Asset> {
        public PublicAssetSchema() {
            AllowFilter(a => a.FileName, QueryOperator.Equal);
        }
    }

    private static readonly Asset[] Assets = [
        new() { Id = 1, FileName = "a.png", OwnerId = 7 },
        new() { Id = 2, FileName = "b.png", OwnerId = 8 },
        new() { Id = 3, FileName = "a.png", OwnerId = 8 }
    ];

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(Action<WebApplication> map) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddQuerying(q => q
            .AddSchema<Asset, AdminAssetSchema>()
            .AddSchema<Asset, PublicAssetSchema>());

        WebApplication app = builder.Build();
        map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return (app, app.GetTestClient());
    }

    private static void MapBoth(WebApplication app) {
        app.MapGet("/admin/assets", (Query<Asset> query, AdminAssetSchema schema) =>
                Results.Ok(Assets.AsQueryable().ApplyQuery(query.Value, schema).Select(a => a.Id).ToArray()))
            .WithQueryValidation<Asset, AdminAssetSchema>();

        app.MapGet("/assets", (Query<Asset> query, PublicAssetSchema schema) =>
                Results.Ok(Assets.AsQueryable().ApplyQuery(query.Value, schema).Select(a => a.Id).ToArray()))
            .WithQueryValidation<Asset, PublicAssetSchema>();
    }

    [Fact]
    public async Task The_Admin_Endpoint_Should_Accept_Its_Owner_Filter() {
        (WebApplication app, HttpClient client) = await StartAsync(MapBoth);
        await using WebApplication _ = app;

        int[]? ids = await client.GetFromJsonAsync<int[]>("/admin/assets?OwnerId=8", TestContext.Current.CancellationToken);

        Assert.Equal([2, 3], ids);
    }

    [Fact]
    public async Task The_Public_Endpoint_Should_Reject_The_Same_Filter() {
        // With one entity-keyed schema this request was validated against AdminAssetSchema and answered.
        (WebApplication app, HttpClient client) = await StartAsync(MapBoth);
        await using WebApplication _ = app;

        HttpResponseMessage response = await client.GetAsync("/assets?OwnerId=8", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("OwnerId", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Binding_Should_Apply_The_Selected_Schemas_Parameter_Rules() {
        // Only AdminAssetSchema ignores "trace"; the binder must read the same schema the validation filter does.
        (WebApplication app, HttpClient client) = await StartAsync(MapBoth);
        await using WebApplication _ = app;

        HttpResponseMessage admin = await client.GetAsync("/admin/assets?FileName=a.png&trace=1", TestContext.Current.CancellationToken);
        HttpResponseMessage pub = await client.GetAsync("/assets?FileName=a.png&trace=1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pub.StatusCode);
    }

    [Fact]
    public async Task A_Group_Should_Apply_Its_Selected_Schema_To_Every_Endpoint() {
        (WebApplication app, HttpClient client) = await StartAsync(app => {
            RouteGroupBuilder group = app.MapGroup("/public").WithQueryValidation<Asset, PublicAssetSchema>();
            group.MapGet("/assets", (Query<Asset> query) => Results.Ok());
        });
        await using WebApplication _ = app;

        HttpResponseMessage allowed = await client.GetAsync("/public/assets?FileName=a.png", TestContext.Current.CancellationToken);
        HttpResponseMessage refused = await client.GetAsync("/public/assets?OwnerId=8", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task An_Endpoint_Selecting_No_Schema_Should_Fail_To_Build_When_The_Entity_Has_Several() {
        (WebApplication app, HttpClient _) = await StartAsync(app =>
            app.MapGet("/assets", (Query<Asset> query) => Results.Ok()).WithQueryValidation<Asset>());
        await using WebApplication __ = app;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            app.Services.GetRequiredService<EndpointDataSource>().Endpoints.ToList());

        Assert.Contains("2 query schemas are registered for Asset", error.Message, StringComparison.Ordinal);
    }
}
