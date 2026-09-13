using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wiaoj.WellKnown.Tests.Integration;

internal sealed class TestApp : IAsyncDisposable {
    private TestApp(WebApplication app) {
        this.App = app;
        this.Client = app.GetTestClient();
    }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static WebApplication Build(Action<IServiceCollection> services, Action<WebApplication> map) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.WebHost.UseTestServer();
        services(builder.Services);

        WebApplication app = builder.Build();
        map(app);
        return app;
    }

    public static async Task<TestApp> StartAsync(Action<IServiceCollection> services, Action<WebApplication> map) {
        WebApplication app = Build(services, map);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestApp(app);
    }

    public async ValueTask DisposeAsync() {
        await this.App.DisposeAsync();
    }
}
