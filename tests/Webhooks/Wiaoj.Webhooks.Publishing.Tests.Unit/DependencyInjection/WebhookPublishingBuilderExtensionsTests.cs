using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Webhooks.Publishing.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Feature", "DependencyInjection")]
[Trait("Component", "GatewayBuilder")]
public sealed class WebhookPublishingBuilderExtensionsTests {

    [Fact]
    public void AddPublishing_RegistersDefaultServicesSuccessfully() {
        ServiceCollection services = new();
        services.AddLogging();

        services.AddWiaojWebhooks(webhooks => {
            webhooks.UseInMemoryTransport()
                    .AddPublishing();
        });

        using ServiceProvider sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IWebhookSubscriptionStore>());
        Assert.NotNull(sp.GetService<IWebhookSubscriptionMatcher>());
        Assert.NotNull(sp.GetService<IWebhookPublisher>());

        Assert.IsType<InMemoryWebhookSubscriptionStore>(sp.GetRequiredService<IWebhookSubscriptionStore>());
        Assert.IsType<WildcardSubscriptionMatcher>(sp.GetRequiredService<IWebhookSubscriptionMatcher>());
        Assert.IsType<WebhookPublisher>(sp.GetRequiredService<IWebhookPublisher>());
    }

    [Fact]
    public void AddPublishing_WithConfigureDelegate_AllowsCustomStoreAndMatcher() {
        ServiceCollection services = new();
        services.AddLogging();

        CustomSubscriptionStore customStore = new();

        services.AddWiaojWebhooks(webhooks => {
            webhooks.AddPublishing(gateway => {
                gateway.UseStore(customStore)
                       .UseMatcher<CustomSubscriptionMatcher>();
            });
        });

        using ServiceProvider sp = services.BuildServiceProvider();

        Assert.Same(customStore, sp.GetRequiredService<IWebhookSubscriptionStore>());
        Assert.IsType<CustomSubscriptionMatcher>(sp.GetRequiredService<IWebhookSubscriptionMatcher>());
    }

    private sealed class CustomSubscriptionStore : IWebhookSubscriptionStore {
        public ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default) {
            return ValueTask.FromResult<IReadOnlyList<WebhookSubscription>>([]);
        }

        public ValueTask SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default) {
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default) {
            return ValueTask.CompletedTask;
        }

        public ValueTask<WebhookSubscription?> GetSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default) {
            return ValueTask.FromResult<WebhookSubscription?>(null);
        }
    }

    private sealed class CustomSubscriptionMatcher : IWebhookSubscriptionMatcher {
        public bool Matches(string pattern, string eventName) {
            return true;
        }

        public bool Matches<TEvent>(WebhookSubscription subscription, string eventName, TEvent payload) where TEvent : IWebhookEvent {
            return true;
        }
    }
}