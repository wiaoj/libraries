using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wiaoj.Querying;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

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
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var databaseName = Guid.NewGuid().ToString();

        builder.Services.AddRouting();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        using(var scope = this._app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            TestDbContext.SeedData(db);
        }

        // Schema configuration with strict whitelist and limits
        var productSchema = new QuerySchema<Product>()
            .AllowFilter(x => x.Id, x => x.Category, x => x.Status)
            .Property(x => x.Price, p => p.AllowFilter(QueryOperator.Equal, QueryOperator.GreaterThanOrEqual, QueryOperator.GreaterThan, QueryOperator.LessThan, QueryOperator.LessThanOrEqual, QueryOperator.Between).AllowSort())
            .AllowFilter(x => x.CreatedAt, x => x.DeletedAt)
            .AllowSort(x => x.Id, x => x.CreatedAt)
            .SearchIn(x => x.Name, x => x.Category)
            .ConfigureLimits(maxFilters: 5, maxInValues: 5, maxSortFields: 3);

        // Minimal API Endpoint Under Test wired with QueryRequestBinder and WithQueryValidation filter
        this._app.MapGet("/api/v1/products", async (Query<Product> query, TestDbContext db) => {  
            var products = await db.Products
                .AsNoTracking()
                .ApplyQuery(query, productSchema)
                .ToListAsync();

            return Results.Ok(products);
        }).WithQueryValidation(productSchema);

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