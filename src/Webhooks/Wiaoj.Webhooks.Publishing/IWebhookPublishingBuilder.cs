using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Fluent builder interface for configuring 1-to-N Webhook Gateway subscription stores, matching strategies, and routing rules.
/// </summary>
public interface IWebhookPublishingBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }
}