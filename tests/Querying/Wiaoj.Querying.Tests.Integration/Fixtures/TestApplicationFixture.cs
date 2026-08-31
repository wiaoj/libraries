using System.Text;
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

public sealed class TestApplicationFixture : IAsyncLifetime {
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;
    public TestServer Server => this._app?.GetTestServer() ?? throw new Exception("Test server not found");

    public async ValueTask InitializeAsync() {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var databaseName = Guid.NewGuid().ToString();

        builder.Services.AddRouting();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        // 1. Yeni IQueryingBuilder mimarimiz ile şemayı kaydediyoruz
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
            });

        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        using(var scope = this._app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            TestDbContext.SeedData(db);
        }

        // 2. Endpoint hem GET, hem QUERY, hem de POST kabul edecek şekilde bağlanır:
        this._app.MapMethods("/api/v1/products", ["GET", "QUERY", "POST"], async (
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