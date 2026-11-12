using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Tests.Unit.Retention;

[Trait("Category", "Unit")]
[Trait("Feature", "Retention")]
[Trait("Component", "DependencyInjection")]
public sealed class WebhookRetentionRegistrationTests {
    [Fact]
    public void UseRetentionPruning_RegistersTheCleanerAsAHostedService() {
        ServiceCollection services = new();

        services.AddWebhooks().UseRetentionPruning(options => options.BatchSize = 10);

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(WebhookRetentionCleanerService));
    }

    [Fact]
    public void UseRetentionPruning_InvalidOptions_ThrowsAtRegistration() {
        ServiceCollection services = new();

        Assert.ThrowsAny<ArgumentException>(() => services.AddWebhooks().UseRetentionPruning(options => options.BatchSize = 0));
    }
}
