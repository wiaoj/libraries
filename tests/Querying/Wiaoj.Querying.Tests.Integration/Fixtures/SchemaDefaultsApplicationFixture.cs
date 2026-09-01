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
/// A second, independently configured test application exercising <see cref="QuerySchema{T}.RequireFilter"/>,
/// <see cref="QuerySchema{T}.DefaultFilter{TProperty}"/>, <see cref="QuerySchema{T}.DefaultSort{TProperty}"/>,
/// and <see cref="QuerySchema{T}.UseCaseInsensitiveText"/> end-to-end over real HTTP requests.
/// </summary>
/// <remarks>
/// Kept deliberately separate from <see cref="TestApplicationFixture"/> — changing the shared schema
/// there would silently alter the expected counts of every existing baseline test in
/// <c>ProductQueryEndpointTests</c>. Reuses the same <see cref="Product"/> entity and
/// <see cref="TestDbContext.SeedData"/> seed set, but points at an isolated in-memory database and
/// a schema that layers three additional, always-applied or fallback rules on top of it:
/// <list type="bullet">
/// <item><description><c>RequireFilter(x => x.DeletedAt == null)</c> — soft-deleted rows are never returned, regardless of what the caller requests.</description></item>
/// <item><description><c>DefaultFilter(x => x.Status, x => x.Status == "Active")</c> — applies only when the caller doesn't filter Status at all.</description></item>
/// <item><description><c>DefaultSort(x => x.CreatedAt, SortDirection.Descending)</c> — applies only when the caller supplies no sort at all.</description></item>
/// <item><description><c>UseCaseInsensitiveText(false)</c> — string comparisons on this endpoint are exact-case, unlike the case-insensitive default used elsewhere.</description></item>
/// </list>
/// </remarks>
public sealed class SchemaDefaultsApplicationFixture : IAsyncLifetime {
    private WebApplication? _app;

    /// <summary>
    /// Gets the configured HTTP client to send requests to this fixture's isolated in-memory test server.
    /// </summary>
    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync() {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var databaseName = Guid.NewGuid().ToString();

        builder.Services.AddRouting();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        builder.Services.AddQuerying()
            .AddSchema<Product>(schema => {
                schema.AllowFilter(x => x.Id, x => x.Category, x => x.Status);
                schema.Property(x => x.Price, p => p.AllowFilter(
                    QueryOperator.Equal,
                    QueryOperator.GreaterThanOrEqual,
                    QueryOperator.GreaterThan,
                    QueryOperator.LessThan,
                    QueryOperator.LessThanOrEqual,
                    QueryOperator.Between).AllowSort());
                schema.AllowFilter(x => x.CreatedAt, x => x.DeletedAt);
                schema.AllowSort(x => x.Id, x => x.CreatedAt);
                schema.SearchIn(x => x.Name, x => x.Category);
                schema.ConfigureLimits(maxFilters: 5, maxInValues: 5, maxSortFields: 3);

                // The three schema-level behaviors this fixture exists to exercise end-to-end:
                schema.RequireFilter(x => x.DeletedAt == null);
                schema.DefaultFilter(x => x.Status, x => x.Status == "Active");
                schema.DefaultSort(x => x.CreatedAt, SortDirection.Descending);
                schema.UseCaseInsensitiveText(false);
            });

        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        using(var scope = this._app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            TestDbContext.SeedData(db);
        }

        this._app.MapGet("/api/v1/products", async (
            Query<Product> query,
            QuerySchema<Product> schema,
            TestDbContext db) => {

                var products = await db.Products
                    .AsNoTracking()
                    .ApplyQuery(query, schema)
                    .ToListAsync();

                return Results.Ok(products);
            }).WithQueryValidation<Product>();

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