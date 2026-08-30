
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        builder.Services.AddRouting();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Configures WebApplication to use in-memory TestServer
        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        // Seed in-memory test database
        using(IServiceScope scope = this._app.Services.CreateScope()) {
            TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            TestDbContext.SeedData(db);
        }

        // Minimal API Endpoint Under Test
        this._app.MapGet("/api/v1/products", async (TestDbContext db) => {
            // Currently returns all products without filtering; .ApplyQuery() will be integrated here
            List<Product> products = await db.Products.AsNoTracking().ToListAsync();
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