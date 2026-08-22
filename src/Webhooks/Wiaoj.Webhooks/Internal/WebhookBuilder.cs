using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Default internal implementation of <see cref="IWebhookBuilder"/>.
/// </summary>
internal sealed class WebhookBuilder : IWebhookBuilder {
    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public WebhookBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;
    }

    /// <inheritdoc/>
    public IWebhookBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IWebhookMiddleware {
        // Otomatik singleton kaydı (Option B)
        this.Services.TryAddSingleton<TMiddleware>();
        this.Services.AddTransient<IWebhookMiddleware>(static sp => sp.GetRequiredService<TMiddleware>());
        return this;
    }

    /// <inheritdoc/>
    public IWebhookBuilder AddMiddleware(IWebhookMiddleware middleware) {
        Preca.ThrowIfNull(middleware);
        this.Services.AddSingleton(middleware);
        this.Services.AddTransient<IWebhookMiddleware>(_ => middleware);
        return this;
    }

    /// <inheritdoc/>
    public IWebhookBuilder AddMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> implementationFactory) where TMiddleware : class, IWebhookMiddleware {
        Preca.ThrowIfNull(implementationFactory);
        this.Services.TryAddSingleton(implementationFactory);
        this.Services.AddTransient<IWebhookMiddleware>(static sp => sp.GetRequiredService<TMiddleware>());
        return this;
    }
}