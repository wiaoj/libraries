using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wiaoj.Webhooks.EntityFrameworkCore.Internal;
using Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.EfCore;

public sealed class RegistrationTests {
    [Fact]
    public void UseEntityFrameworkCoreStore_ReplacesTheJobStoreWithASingleton() {
        ServiceCollection services = new();

        services.AddWebhooks().UseEntityFrameworkCoreStore<TestWebhookDbContext>();

        ServiceDescriptor store = Assert.Single(services, d => d.ServiceType == typeof(IWebhookStore));
        Assert.Equal(typeof(EfCoreWebhookStore<TestWebhookDbContext>), store.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, store.Lifetime);
    }

    [Fact]
    public void PublishingUseEntityFrameworkCoreStore_RegistersSubscriptionAndBatchStoresAndBatchRecovery() {
        ServiceCollection services = new();

        services.AddWebhooks().AddPublishing(publishing => publishing.UseEntityFrameworkCoreStore<TestWebhookDbContext>());

        Assert.Equal(typeof(EfCoreWebhookSubscriptionStore<TestWebhookDbContext>),
            Assert.Single(services, d => d.ServiceType == typeof(IWebhookSubscriptionStore)).ImplementationType);
        Assert.Equal(typeof(EfCoreWebhookBatchStore<TestWebhookDbContext>),
            Assert.Single(services, d => d.ServiceType == typeof(IWebhookBatchStore)).ImplementationType);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "StaleBatchRecoveryService");
    }

    [Fact]
    public void UseEntityFrameworkCoreStore_NullBuilder_Throws() {
        Assert.ThrowsAny<ArgumentNullException>(() => WebhookBuilderEntityFrameworkCoreExtensions.UseEntityFrameworkCoreStore<TestWebhookDbContext>(null!));
        Assert.ThrowsAny<ArgumentNullException>(() => WebhookPublishingBuilderEntityFrameworkCoreExtensions.UseEntityFrameworkCoreStore<TestWebhookDbContext>(null!));
    }

    [Fact]
    public async Task AContextWithoutApplyWebhooks_FailsWithAnExplanation() {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ServiceCollection services = new();
        services.AddDbContext<UnmappedDbContext>(options => options.UseSqlite(connection));
        services.AddWebhooks().UseEntityFrameworkCoreStore<UnmappedDbContext>();
        await using ServiceProvider provider = services.BuildServiceProvider();

        IWebhookStore store = provider.GetRequiredService<IWebhookStore>();
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetJobAsync(WebhookJobId.NewJobId(), TestContext.Current.CancellationToken));

        Assert.Contains("ApplyWebhooks", error.Message);
    }
}
