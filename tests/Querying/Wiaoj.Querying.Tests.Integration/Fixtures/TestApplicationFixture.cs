
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wiaoj.Querying.Extensions;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Integration.Fixtures;
/// <summary>
/// Modern in-memory test host fixture utilizing <see cref="WebApplicationBuilder"/> and <see cref="TestServer"/>.
/// </summary>
public sealed class TestApplicationFixture : IAsyncLifetime {
    private WebApplication? _app;

    /// <summary>
    /// Gets the configured HTTP client to send requests to the in-memory test server.
    /// </summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>
    /// Gets the in-memory test server instance.
    /// </summary>
    public TestServer Server => this._app!.GetTestServer();

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var databaseName = Guid.NewGuid().ToString();

        builder.Services.AddRouting();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        // Seed in-memory test database
        using(IServiceScope scope = this._app.Services.CreateScope()) {
            TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            TestDbContext.SeedData(db);
        }

        // Configure query schema for Product entity
        QuerySchema<Product> productSchema = new QuerySchema<Product>()
            .AllowFilter(x => x.Id, x => x.Price, x => x.CreatedAt)
            .AllowFilter(x => x.Category, x => x.Status, x => x.Name)
            .AllowSort(x => x.Id, x => x.Price, x => x.CreatedAt)
            .SearchIn(x => x.Name, x => x.Category);

        // Minimal API Endpoint Under Test with structured querying applied
        this._app.MapGet("/api/v1/products", async (HttpContext context, TestDbContext db) => {
            Q q = default;
            Sort sort = default;
            List<FilterConditionNode>? filters = null;

            foreach((string? key, Microsoft.Extensions.Primitives.StringValues stringValues) in context.Request.Query) {
                if(string.IsNullOrEmpty(key)) {
                    continue;
                }

                if(key.Equals("q", StringComparison.OrdinalIgnoreCase)) {
                    q = new Q(stringValues.ToString());
                    continue;
                }

                if(key.Equals("sort", StringComparison.OrdinalIgnoreCase)) {
                    sort = new Sort(stringValues.ToString());
                    continue;
                }

                foreach(var val in stringValues) {
                    var rawPair = string.IsNullOrEmpty(val) ? key : $"{key}={val}";
                    if(BracketQueryParser.TryParse(rawPair, out FilterConditionNode filterNode)) {
                        filters ??= [];
                        filters.Add(filterNode);
                    }
                }
            }

            var queryRequest = new QueryRequest(q: q, sort: sort, filters: filters);

            List<Product> products = await db.Products
                .AsNoTracking()
                .ApplyQuery(queryRequest, productSchema)
                .ToListAsync();

            return Results.Ok(products);
        });

        await this._app.StartAsync();
        this.Client = this._app.GetTestClient();
    }

    public async ValueTask DisposeAsync() {
        if(this._app != null) {
            await this._app.StopAsync();
            await this._app.DisposeAsync();
        }
    }
}