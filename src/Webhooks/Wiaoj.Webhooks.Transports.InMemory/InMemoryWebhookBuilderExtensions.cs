using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// Extension methods for configuring the in-memory transport directly on <see cref="IWebhookBuilder"/>.
/// </summary>
public static class InMemoryWebhookBuilderExtensions {
    /// <summary>
    /// Configures the native in-memory webhook transport with default settings and registers the multi-worker consumer pool.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The same webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.AddInMemoryWebhookTransport();
        return builder;
    }

    /// <summary>
    /// Configures the native in-memory webhook transport with bounded channel capacity and registers the multi-worker consumer pool.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="capacity">The maximum number of unprocessed jobs buffered before backpressure is applied.</param>
    /// <returns>The same webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder, int capacity) {
        Preca.ThrowIfNull(builder);
        builder.Services.AddInMemoryWebhookTransport(capacity);
        return builder;
    }

    /// <summary>
    /// Configures the native in-memory webhook transport with a delegate and registers the multi-worker consumer pool.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="configure">The delegate to configure options.</param>
    /// <returns>The same webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder, Action<InMemoryWebhookTransportOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        builder.Services.AddInMemoryWebhookTransport(configure);
        return builder;
    }

    /// <summary>
    /// Configures the native in-memory webhook transport with options and registers the multi-worker consumer pool.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="options">The configuration options.</param>
    /// <returns>The same webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder, InMemoryWebhookTransportOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        builder.Services.AddInMemoryWebhookTransport(options);
        return builder;
    }
}
