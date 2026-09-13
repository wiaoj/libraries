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

    private static CursorResult<Row> Window() {
        return new(
            new EquatableArray<Row>([new Row(1)]),
            new CursorMetadata(CursorToken.FromUtf8("a"), CursorToken.FromUtf8("b"), hasPrevious: false, hasNext: true));
    }

    private static string[] ParameterNames(JsonElement operation) {
        return operation.TryGetProperty("parameters", out JsonElement parameters)
            ? [.. parameters.EnumerateArray().Select(p => p.GetProperty("name").GetString()!)]
            : [];
    }

    /// <summary>
    /// <c>WithPagination(PaginationStyle)</c>, for a handler whose return type cannot say which style it serves.
    /// The statement is checked wherever something can contradict it — the handler's return type when the
    /// endpoint is built, the actual response at run time, a <c>Produces&lt;T&gt;()</c> in the document — so it
    /// is never an assertion that drifts from the endpoint.
    /// </summary>
    public sealed class TheDeclaredStyle {
        [Fact]
        public async Task Should_Document_An_Opaque_Endpoint_As_The_Style_It_Declared() {
            // Without the declaration, IResult carries no shape and the document describes no paging at all.
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/assets", IResult () => TypedResults.Ok(Window()))
                    .WithPagination(PaginationStyle.Cursor));

            JsonElement operation = await host.OperationAsync("/assets");
            string[] parameters = ParameterNames(operation);

            Assert.Contains(PaginationParameters.Cursor, parameters);
            Assert.Contains(PaginationParameters.Limit, parameters);
            Assert.Contains(PaginationParameters.Direction, parameters);
            Assert.DoesNotContain(PaginationParameters.Page, parameters);
            Assert.True(DocumentsHeader(operation, "Link"));
            Assert.True(DocumentsHeader(operation, "ETag"));
        }

        [Fact]
        public async Task Should_Still_Page_The_Response() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/assets", IResult () => TypedResults.Ok(Window()))
                    .WithPagination(PaginationStyle.Cursor));

            HttpResponseMessage response = await host.GetAsync("/assets");

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("cursor=", string.Join(",", response.Headers.GetValues("Link")), StringComparison.Ordinal);
            Assert.NotNull(response.Headers.ETag);
        }

        [Fact]
        public async Task Should_Refine_The_Application_Settings_When_Configured() {
            await using Host host = await Host.StartAsync(
                services => services.AddPagination(options => options.EnableETag = false),
                app => app.MapGet("/assets", IResult () => TypedResults.Ok(Window()))
                    .WithPagination(PaginationStyle.Cursor, options => options.EnableLinkHeaders = false));

            HttpResponseMessage response = await host.GetAsync("/assets");
            JsonElement operation = await host.OperationAsync("/assets");

            Assert.False(response.Headers.Contains("Link"));
            Assert.Null(response.Headers.ETag);
            Assert.False(DocumentsHeader(operation, "Link"));
            Assert.False(DocumentsHeader(operation, "ETag"));
        }

        [Fact]
        public async Task Should_Fail_At_Build_When_The_Return_Type_Serves_The_Other_Style() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/rows", () => TypedResults.Ok(Page()))
                    .WithPagination(PaginationStyle.Cursor));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.BuildEndpoints().ToList());

            Assert.Contains("PaginationStyle.Cursor", error.Message, StringComparison.Ordinal);
            Assert.Contains("serves Offset", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Fail_At_Build_When_The_Return_Type_Is_Not_A_Page() {
            // The filter would never recognise the response, so the endpoint would silently not be paginated.
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/workspace", () => TypedResults.Ok(new Workspace("verba", [], SecondOfFive)))
                    .WithPagination(PaginationStyle.Offset));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.BuildEndpoints().ToList());

            Assert.Contains("WithPagination<TResponse>", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Accept_A_Declaration_The_Return_Type_Agrees_With() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/assets", Results<Ok<CursorResult<Row>>, ProblemHttpResult> () => TypedResults.Ok(Window()))
                    .WithPagination(PaginationStyle.Cursor));

            HttpResponseMessage response = await host.GetAsync("/assets");
            JsonElement operation = await host.OperationAsync("/assets");

            Assert.True(response.Headers.Contains("Link"));
            Assert.Contains(PaginationParameters.Cursor, ParameterNames(operation));
        }

        [Fact]
        public async Task Should_Fail_The_Response_When_An_Opaque_Handler_Returns_The_Other_Style() {
            // Nothing can be checked at build. Serving the page anyway would leave the document advertising
            // cursor parameters this endpoint ignores, indefinitely; failing surfaces it on the first call.
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/rows", IResult () => TypedResults.Ok(Page()))
                    .WithPagination(PaginationStyle.Cursor));

            HttpResponseMessage response = await host.GetAsync("/rows");
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains(nameof(InvalidOperationException), body, StringComparison.Ordinal);
            Assert.Contains("declared PaginationStyle.Cursor pagination but returned PagedResult", body, StringComparison.Ordinal);
            Assert.False(response.Headers.Contains("Link"));
        }

        [Fact]
        public async Task Should_Leave_A_Response_That_Is_Not_A_Page_Alone() {
            // A problem is not a contradiction of the declared style — it is not a page of any style.
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/assets", IResult () => TypedResults.Problem("not found", statusCode: 404))
                    .WithPagination(PaginationStyle.Cursor));

            HttpResponseMessage response = await host.GetAsync("/assets");

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(response.Headers.Contains("Link"));
            Assert.Null(response.Headers.ETag);
        }

        [Fact]
        public async Task Should_Fail_The_Document_When_Produces_Contradicts_It() {
            // An opaque handler passes the build, so the documented response type is the only thing to check.
            await using Host host = await Host.StartAsync(
                services => { },
                app => app.MapGet("/rows", IResult () => TypedResults.Ok(Page()))
                    .Produces<PagedResult<Row>>()
                    .WithPagination(PaginationStyle.Cursor));

            HttpResponseMessage response = await host.GetAsync("/openapi/v1.json");
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains("declared PaginationStyle.Cursor pagination, but its response type is documented as Offset", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Apply_To_Every_Endpoint_In_A_Group() {
            await using Host host = await Host.StartAsync(
                services => { },
                app => {
                    RouteGroupBuilder group = app.MapGroup("/timeline").WithPagination(PaginationStyle.Cursor);
                    group.MapGet("/", IResult () => TypedResults.Ok(Window()));
                });

            HttpResponseMessage response = await host.GetAsync("/timeline");
            JsonElement operation = await host.OperationAsync("/timeline");

            Assert.True(response.Headers.Contains("Link"));
            Assert.Contains(PaginationParameters.Cursor, ParameterNames(operation));
        }

        [Fact]
        public void Should_Reject_An_Undefined_Style() {
            WebApplication app = WebApplication.CreateBuilder().Build();

            Assert.ThrowsAny<ArgumentException>(() => app.MapGet("/x", () => 1).WithPagination((PaginationStyle)42));
        }
    }
}
