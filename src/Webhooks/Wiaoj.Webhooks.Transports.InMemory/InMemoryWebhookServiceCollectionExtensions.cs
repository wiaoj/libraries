using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up the native in-memory webhook transport in an <see cref="IServiceCollection"/>.
/// </summary>
public static class InMemoryWebhookServiceCollectionExtensions {
    /// <summary>
    /// Adds the native in-memory webhook transport with default settings and starts the multi-worker consumer service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddInMemoryWebhookTransport(this IServiceCollection services) {
        return AddInMemoryWebhookTransport(services, new InMemoryWebhookTransportOptions());
    }

    /// <summary>
    /// Adds the native in-memory webhook transport with bounded channel capacity and starts the multi-worker consumer service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="capacity">The maximum number of unprocessed jobs buffered before backpressure is applied.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddInMemoryWebhookTransport(this IServiceCollection services, int capacity) {
        Preca.ThrowIfLessThan(capacity, 1);
        InMemoryWebhookTransportOptions options = new() { Capacity = capacity };
        return AddInMemoryWebhookTransport(services, options);
    }

    /// <summary>
    /// Adds the native in-memory webhook transport configured with a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The delegate to configure options.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddInMemoryWebhookTransport(this IServiceCollection services, Action<InMemoryWebhookTransportOptions> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        InMemoryWebhookTransportOptions options = new();
        configure(options);
        return AddInMemoryWebhookTransport(services, options);
    }

    /// <summary>
    /// Adds the native in-memory webhook transport with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The transport configuration options.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddInMemoryWebhookTransport(this IServiceCollection services, InMemoryWebhookTransportOptions options) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(options);

        services.RemoveAll<IWebhookTransport>();
        services.RemoveAll<InMemoryWebhookTransport>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.AddSingleton(sp => new InMemoryWebhookTransport(options, sp.GetRequiredService<ILogger<InMemoryWebhookTransport>>()));
        services.AddSingleton<IWebhookTransport>(sp => sp.GetRequiredService<InMemoryWebhookTransport>());
        services.AddHostedService<InMemoryWebhookConsumer>();

        return services;
    }
}
