using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;

/// <summary>
/// Builds the webhook services on one <see cref="StoreBackend"/> and exposes its stores, with a controllable clock.
/// </summary>
public abstract class StoreTestBase : IAsyncLifetime {
    private ServiceProvider? _provider;

    protected StoreBackend Backend { get; private set; } = null!;

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    protected IServiceProvider Services => this._provider ?? throw new InvalidOperationException("The provider is not built.");

    protected IWebhookStore JobStore => this.Services.GetRequiredService<IWebhookStore>();

    protected IWebhookSubscriptionStore SubscriptionStore => this.Services.GetRequiredService<IWebhookSubscriptionStore>();

    protected IWebhookBatchStore BatchStore => this.Services.GetRequiredService<IWebhookBatchStore>();

    protected abstract StoreBackend CreateBackend();

    public async ValueTask InitializeAsync() {
        this.Backend = CreateBackend();
        Assert.SkipWhen(this.Backend.SkipReason is not null, this.Backend.SkipReason ?? string.Empty);

        this._provider = BuildProvider();
        await this.Backend.CreateSchemaAsync(this._provider);
    }

    /// <summary>
    /// Disposes the service provider and builds a new one on the same backend, as a process restart would.
    /// </summary>
    protected async Task RestartAsync() {
        if(this._provider is not null) {
            await this._provider.DisposeAsync();
        }

        this._provider = BuildProvider();
    }

    protected virtual void ConfigureServices(IServiceCollection services) {
    }

    private ServiceProvider BuildProvider() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(this.Time);
        this.Backend.ConfigureServices(services);

        IWebhookBuilder webhooks = services.AddWebhooks();
        this.Backend.ConfigureWebhooks(webhooks);
        webhooks.AddPublishing(this.Backend.ConfigurePublishing);

        ConfigureServices(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    public async ValueTask DisposeAsync() {
        if(this._provider is not null) {
            await this._provider.DisposeAsync();
        }

        if(this.Backend is not null) {
            await this.Backend.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
