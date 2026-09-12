using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Wiaoj.Pagination.AspNetCore;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.OpenApi.Tests.Integration;

/// <summary>
/// Application-wide settings, per-endpoint refinement, and envelope responses — each checked at run time and
/// in the generated document, because the two are resolved from the same metadata and must agree.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Pagination")]
[Trait("Component", "Configuration")]
public sealed class PaginationConfigurationTests {

    internal sealed record Row(int Id);

    /// <summary>A paged response carrying more than the page, as a workspace view does.</summary>
    internal sealed record Workspace(string Project, IReadOnlyList<Row> Items, PageMetadata Metadata);

    internal sealed record Timeline(string Owner, IReadOnlyList<Row> Items, CursorMetadata Window);

    private static readonly PageMetadata SecondOfFive = new(totalCount: 50, page: 2, size: 10);

    private sealed class Host : IAsyncDisposable {
        private readonly WebApplication _app;

        private Host(WebApplication app) {
            this._app = app;
            this.Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<Host> StartAsync(Action<IServiceCollection> services, Action<WebApplication> map) {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Development
            });

            builder.Logging.SetMinimumLevel(LogLevel.Critical);
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddOpenApi(options => options.AddWiaojPagination());
            services(builder.Services);

            WebApplication app = builder.Build();
            map(app);
            app.MapOpenApi();

            await app.StartAsync();
            return new Host(app);
        }

        public async Task<HttpResponseMessage> GetAsync(string path) {
            return await this.Client.GetAsync(path, TestContext.Current.CancellationToken);
        }

        public async Task<JsonElement> OperationAsync(string path) {
            string json = await this.Client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
            return JsonDocument.Parse(json).RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");
        }

        public IEnumerable<Endpoint> BuildEndpoints() {
            return this._app.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        }

        public async ValueTask DisposeAsync() {
            await this._app.DisposeAsync();
        }
    }

    private static PagedResult<Row> Page() => new(new EquatableArray<Row>([new Row(11)]), SecondOfFive);

    private static bool DocumentsHeader(JsonElement operation, string header) {
        return operation.GetProperty("responses").GetProperty("200").TryGetProperty("headers", out JsonElement headers)
            && headers.TryGetProperty(header, out _);
    }

    public sealed class TheApplicationSettings {
        [Fact]
        public async Task Should_Apply_To_An_Endpoint_That_Configures_Nothing() {
            await using Host host = await Host.StartAsync(
                services => services.AddPagination(options => options.EnableETag = false),
                app => app.MapGet("/rows", () => TypedResults.Ok(Page())).WithPagination());

            HttpResponseMessage response = await host.GetAsync("/rows");
            JsonElement operation = await host.OperationAsync("/rows");

            Assert.Null(response.Headers.ETag);
            Assert.False(DocumentsHeader(operation, "ETag"));
            Assert.False(operation.GetProperty("responses").TryGetProperty("304", out _));
        }

        [Fact]
        public async Task Should_Leave_The_Library_Defaults_When_Never_Registered() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/rows", () => TypedResults.Ok(Page())).WithPagination());

            HttpResponseMessage response = await host.GetAsync("/rows");

            Assert.NotNull(response.Headers.ETag);
            Assert.True(response.Headers.Contains("Link"));
        }

        [Fact]
        public async Task An_Endpoint_Should_Refine_Them_Rather_Than_Start_From_Defaults() {
            // The application turned ETags off; the endpoint only turns Link headers off. If the endpoint's
            // callback applied to fresh defaults, ETags would silently come back on here.
            await using Host host = await Host.StartAsync(
                services => services.AddPagination(options => options.EnableETag = false),
                app => app.MapGet("/rows", () => TypedResults.Ok(Page()))
                    .WithPagination(options => options.EnableLinkHeaders = false));

            HttpResponseMessage response = await host.GetAsync("/rows");
            JsonElement operation = await host.OperationAsync("/rows");

            Assert.Null(response.Headers.ETag);
            Assert.False(response.Headers.Contains("Link"));
            Assert.False(DocumentsHeader(operation, "ETag"));
            Assert.False(DocumentsHeader(operation, "Link"));
        }
    }

    /// <summary>
    /// A response that carries a page beside other data, declared at the endpoint. The response type stays a
    /// plain record — it implements nothing from this library.
    /// </summary>
    public sealed class TheEnvelope {
        [Fact]
        public async Task Should_Get_Link_Headers_And_An_ETag_From_The_Declared_Metadata() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", () => TypedResults.Ok(new Workspace("verba", [new Row(11)], SecondOfFive)))
                    .WithPagination<Workspace>(workspace => workspace.Metadata));

            HttpResponseMessage response = await host.GetAsync("/workspace");
            string link = string.Join(",", response.Headers.GetValues("Link"));

            Assert.Contains("rel=\"next\"", link, StringComparison.Ordinal);
            Assert.Contains("rel=\"prev\"", link, StringComparison.Ordinal);
            Assert.NotNull(response.Headers.ETag);
        }

        [Fact]
        public async Task Should_See_Through_A_Union_Return_Type() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", Results<Ok<Workspace>, ProblemHttpResult> () =>
                        TypedResults.Ok(new Workspace("verba", [new Row(11)], SecondOfFive)))
                    .WithPagination<Workspace>(workspace => workspace.Metadata));

            HttpResponseMessage response = await host.GetAsync("/workspace");

            Assert.True(response.Headers.Contains("Link"));
        }

        [Fact]
        public async Task Should_Work_For_A_Keyset_Envelope() {
            CursorMetadata window = new(CursorToken.FromUtf8("a"), CursorToken.FromUtf8("b"), hasPrevious: false, hasNext: true);

            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/timeline", () => TypedResults.Ok(new Timeline("verba", [new Row(1)], window)))
                    .WithPagination<Timeline>(timeline => timeline.Window));

            HttpResponseMessage response = await host.GetAsync("/timeline");

            Assert.Contains("cursor=", string.Join(",", response.Headers.GetValues("Link")), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Be_Documented_As_The_Style_It_Declared() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", () => TypedResults.Ok(new Workspace("verba", [new Row(11)], SecondOfFive)))
                    .WithPagination<Workspace>(workspace => workspace.Metadata));

            JsonElement operation = await host.OperationAsync("/workspace");
            string[] parameters = [.. operation.GetProperty("parameters").EnumerateArray()
                .Select(p => p.GetProperty("name").GetString()!)];

            Assert.Contains(PaginationParameters.Page, parameters);
            Assert.Contains(PaginationParameters.Size, parameters);
            Assert.True(DocumentsHeader(operation, "Link"));
        }

        [Fact]
        public async Task Should_Leave_A_Problem_Response_Alone() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", Results<Ok<Workspace>, ProblemHttpResult> () =>
                        TypedResults.Problem("not found", statusCode: 404))
                    .WithPagination<Workspace>(workspace => workspace.Metadata));

            HttpResponseMessage response = await host.GetAsync("/workspace");

            Assert.False(response.Headers.Contains("Link"));
            Assert.Null(response.Headers.ETag);
        }

        [Fact]
        public async Task Should_Fail_At_Build_When_The_Declared_Type_Is_Not_What_The_Handler_Returns() {
            // Otherwise the accessor would never match, and the endpoint would quietly send no Link header.
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", () => TypedResults.Ok(new Workspace("verba", [], SecondOfFive)))
                    .WithPagination<Timeline>(timeline => timeline.Window));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.BuildEndpoints().ToList());

            Assert.Contains(nameof(Timeline), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Accept_An_Opaque_Return_Type_It_Cannot_Check() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", IResult () => TypedResults.Ok(new Workspace("verba", [new Row(1)], SecondOfFive)))
                    .WithPagination<Workspace>(workspace => workspace.Metadata));

            HttpResponseMessage response = await host.GetAsync("/workspace");

            Assert.True(response.Headers.Contains("Link"));
        }
    }
}
