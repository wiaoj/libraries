using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.TestData;

internal static class GatewayTestFactory {
    public static WebhookPublisher CreateGateway(
        IWebhookSubscriptionStore? store = null,
        IWebhookSubscriptionMatcher? matcher = null,
        IWebhookDispatcher? dispatcher = null,
        IWebhookEventRegistry? eventRegistry = null,
        IWebhookBatchStore? batchStore = null,
        TimeProvider? timeProvider = null) {

        return new WebhookPublisher(
            store ?? new InMemoryWebhookSubscriptionStore(),
            matcher ?? new WildcardSubscriptionMatcher(),
            dispatcher ?? new FakeWebhookDispatcher(),
            eventRegistry ?? new WebhookEventRegistry(new WebhookEventRegistryOptions()),
            batchStore ?? NullWebhookBatchStore.Instance,
            new SystemTextJsonSerializer<WebhookSerializerKey>(),
            timeProvider ?? TimeProvider.System,
            NullLogger<WebhookPublisher>.Instance);
    }
}