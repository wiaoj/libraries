using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.DependencyInjection;

public sealed class DependencyInjectionTests {
    [Fact]
    public void AddWiaojWebhooks_RegistersCoreServices_AndBuildsServiceProvider() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ISerializer<WebhookSerializerKey>, FakeWebhookSerializer>();
        services.AddSingleton<ISecretProtector<WebhookSigningContext>, FakeSecretProtector<WebhookSigningContext>>();
        services.AddSingleton<IWebhookEndpointResolver, FakeWebhookEndpointResolver>();
        services.AddInMemoryWebhookTransport();

        services.AddWiaojWebhooks(builder => {
            builder.UseHmacSha256Signing()
                   .UseExponentialBackoffRetry();
        });

        using ServiceProvider provider = services.BuildServiceProvider();

        IWebhookDispatcher dispatcher = provider.GetRequiredService<IWebhookDispatcher>();
        IWebhookJobHandler jobHandler = provider.GetRequiredService<IWebhookJobHandler>();
        IWebhookTransport transport = provider.GetRequiredService<IWebhookTransport>();
        IWebhookStore store = provider.GetRequiredService<IWebhookStore>();
        IWebhookSigner signer = provider.GetRequiredService<IWebhookSigner>();
        IWebhookRetryPolicy retryPolicy = provider.GetRequiredService<IWebhookRetryPolicy>();
        IEnumerable<IWebhookMiddleware> middlewares = provider.GetServices<IWebhookMiddleware>();

        Assert.NotNull(dispatcher);
        Assert.NotNull(jobHandler);
        Assert.NotNull(transport);
        Assert.IsType<InMemoryWebhookStore>(store);
        Assert.IsType<HmacSha256WebhookSigner>(signer);
        Assert.IsType<ExponentialBackoffPolicy>(retryPolicy);
        Assert.Equal(2, middlewares.Count()); // SigningMiddleware + RetryMiddleware
    }

    [Fact]
    public void Builder_AllowsCustomSignerAndRetryPolicies() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ISerializer<WebhookSerializerKey>, FakeWebhookSerializer>();
        services.AddSingleton<ISecretProtector<WebhookSigningContext>, FakeSecretProtector<WebhookSigningContext>>();
        services.AddSingleton<IWebhookEndpointResolver, FakeWebhookEndpointResolver>();
        services.AddInMemoryWebhookTransport();

        services.AddWiaojWebhooks(builder => {
            builder.UseHmacSha512Signing()
                   .UseLinearBackoffRetry(4, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
        });

        using ServiceProvider provider = services.BuildServiceProvider();

        IWebhookSigner signer = provider.GetRequiredService<IWebhookSigner>();
        IWebhookRetryPolicy retryPolicy = provider.GetRequiredService<IWebhookRetryPolicy>();

        Assert.IsType<HmacSha512WebhookSigner>(signer);
        Assert.IsType<LinearBackoffPolicy>(retryPolicy);
    }

    [Fact]
    public void Builder_StoreExtensions_RegisterCustomAndNullStoresCorrectly() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ISerializer<WebhookSerializerKey>, FakeWebhookSerializer>();
        services.AddSingleton<ISecretProtector<WebhookSigningContext>, FakeSecretProtector<WebhookSigningContext>>();
        services.AddSingleton<IWebhookEndpointResolver, FakeWebhookEndpointResolver>();
        services.AddInMemoryWebhookTransport();

        services.AddWebhooks(builder => {
            builder.UseNullStore();
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IWebhookStore store = provider.GetRequiredService<IWebhookStore>();
        Assert.IsType<NullWebhookStore>(store);
    }

    [Fact]
    public void Builder_TransportExtensions_ConfigureOptionsCorrectly() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<ISerializer<WebhookSerializerKey>, FakeWebhookSerializer>();
        services.AddSingleton<ISecretProtector<WebhookSigningContext>, FakeSecretProtector<WebhookSigningContext>>();
        services.AddSingleton<IWebhookEndpointResolver, FakeWebhookEndpointResolver>();

        services.AddWebhooks(builder => {
            builder.UseInMemoryTransport(opts => {
                opts.Concurrency = 16;
                opts.Capacity = 500;
            });
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<InMemoryWebhookTransportOptions> options = provider.GetRequiredService<IOptions<InMemoryWebhookTransportOptions>>();

        Assert.Equal(16, options.Value.Concurrency);
        Assert.Equal(500, options.Value.Capacity);
    }
}