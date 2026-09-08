using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Wiaoj.Pagination.AspNetCore;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.OpenApi.Tests.Integration.Fixtures;

/// <summary>The item type the paged endpoints return, so the document has something to point at.</summary>
public sealed record Product(int Id, string Name);

/// <summary>
/// Hosts an application that pages, and hands the tests the document its own generator produced.
/// </summary>
/// <remarks>
/// The unit tests over the transformers can only assert what the transformers were asked to write. What
/// broke in practice was a step further out: a type whose schema the generator declined to build at all, so
/// nothing was there for a transformer to be right about. The only way to see that is to read the document
/// the generator emits.
/// </remarks>
public sealed class DocumentFixture : IAsyncLifetime {
    private WebApplication? _app;

    /// <summary>Gets the generated OpenAPI document, parsed.</summary>
    public JsonDocument Document { get; private set; } = null!;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddRouting();
        builder.Services.AddOpenApi(options => options.AddWiaojPagination());
        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        this._app.MapGet("/offset", () => Page()).WithPagination();
        this._app.MapGet("/keyset", (CursorParameters paging) => Window()).WithPagination();
        this._app.MapGet("/compact", (CursorRequest paging) => Window()).WithPagination();

        // The shape a handler returning a union declares. Its page sits two wrappers in, and the transformer
        // has to see through both to know this endpoint pages at all.
        this._app.MapGet("/union", Results<Ok<CursorResult<Product>>, ProblemHttpResult> () =>
            TypedResults.Ok(Window())).WithPagination();
        this._app.MapOpenApi();

        await this._app.StartAsync();

        using HttpClient client = this._app.GetTestClient();
        string json = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        this.Document = JsonDocument.Parse(json);
    }

    public async ValueTask DisposeAsync() {
        this.Document?.Dispose();

        if(this._app is not null) {
            await this._app.DisposeAsync();
        }
    }

    /// <summary>Gets a schema out of the document's component section.</summary>
    public JsonElement Schema(string name) {
        return this.Document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(name);
    }

    /// <summary>Gets the parameters declared for an operation.</summary>
    public JsonElement Parameters(string path) {
        return this.Document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty("get")
            .GetProperty("parameters");
    }

    private static PagedResult<Product> Page() {
        return new PagedResult<Product>(
            new EquatableArray<Product>([new Product(1, "first")]),
            new PageMetadata(totalCount: 1, page: 1, size: 20));
    }

    private static CursorResult<Product> Window() {
        return new CursorResult<Product>(
            new EquatableArray<Product>([new Product(1, "first")]),
            CursorMetadata.Empty);
    }
}
