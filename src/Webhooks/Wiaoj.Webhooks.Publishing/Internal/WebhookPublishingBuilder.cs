using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// Default internal implementation of <see cref="IWebhookPublishingBuilder"/>.
/// </summary>
internal sealed class WebhookPublishingBuilder : IWebhookPublishingBuilder {
    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPublishingBuilder"/> class.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    public WebhookPublishingBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;
    }
}