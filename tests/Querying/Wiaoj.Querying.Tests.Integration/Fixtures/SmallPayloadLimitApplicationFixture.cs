using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wiaoj.Querying.AspNetCore;
using Wiaoj.Querying.Extensions;

namespace Wiaoj.Querying.Tests.Integration.Fixtures;

/// <summary>
/// A third, independently configured test application with a deliberately tiny
/// <see cref="QueryOptions.MaxPayloadBytes"/> override (50 bytes), proving the application-wide
/// ceiling actually takes effect end-to-end over HTTP — not just that the property exists.
/// </summary>
/// <remarks>
/// NOTE: this assumes <c>AddQuerying(Action&lt;OptionsBuilder&lt;QueryOptions&gt;&gt;)</c> returns an
/// <see cref="IQueryingBuilder"/> so it can be chained directly into <c>.AddSchema&lt;T&gt;(...)</c>,
/// mirroring the pattern seen in <c>QueryingBuilderTests</c>. If the real signature doesn't support
/// chaining, split this into two statements: configure options first, then <c>AddSchema</c> separately.
/// </remarks>
public sealed class SmallPayloadLimitApplicationFixture : IAsyncLifetime {
    private WebApplication? _app;

    /// <summary>
    /// Gets the configured HTTP client to send requests to this fixture's isolated in-memory test server.
    /// </summary>
    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var databaseName = Guid.NewGuid().ToString();

        builder.Services.AddRouting();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        builder.Services
            .AddQuerying(options => {
                options.Configure(x => x.MaxPayloadBytes = 50);
            })
            .AddSchema<Product>(schema => {
                schema.AllowFilter(x => x.Id, x => x.Category, x => x.Status);
                schema.Property(x => x.Price, p => p.AllowFilter(
                    QueryOperator.Equal,
                    QueryOperator.GreaterThanOrEqual).AllowSort());
                schema.SearchIn(x => x.Name, x => x.Category);
                schema.ConfigureLimits(maxFilters: 5, maxInValues: 5, maxSortFields: 3);
            });

        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        using(IServiceScope scope = this._app.Services.CreateScope()) {
            TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            TestDbContext.SeedData(db);
        }

        this._app.MapMethods("/api/v1/products", ["GET", "QUERY", "POST"], async (
            Query<Product> query,
            QuerySchema<Product> schema,
            TestDbContext db) => {

                List<Product> products = await db.Products
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