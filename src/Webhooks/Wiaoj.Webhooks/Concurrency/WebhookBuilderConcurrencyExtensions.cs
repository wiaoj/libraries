using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Concurrency;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring per-endpoint concurrency serialization and partitioning locks on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderConcurrencyExtensions {
    /// <summary>
    /// Configures serialized outbound deliveries per <see cref="WebhookEndpointId"/> using zero-collision dynamic mailboxes.
    /// </summary>
    /// <remarks>
    /// Guarantees strict FIFO delivery per endpoint using an atomic reference-counted lock.
    /// Distinct endpoints execute concurrently in parallel without cross-blocking.
    /// </remarks>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UsePartitionedDelivery(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookDeliveryLock>();
        builder.Services.AddSingleton<IWebhookDeliveryLock, EndpointMailboxDeliveryLock>();
        builder.AddMiddleware<PartitionedDeliveryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures serialized outbound deliveries per <see cref="WebhookEndpointId"/> using fixed power-of-two striped locks.
    /// </summary>
    /// <remarks>
    /// Backed by a pre-allocated array of non-blocking asynchronous stripes.
    /// Ideal for single-node environments with bounded memory footprints.
    /// </remarks>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="stripeCount">The number of power-of-two lock stripes (default is 4096). Must be a power of two.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stripeCount"/> is less than 1 or not a power of two.</exception>
    public static IWebhookBuilder UseStripedPartitionedDelivery(this IWebhookBuilder builder, int stripeCount = 4096) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookDeliveryLock>();
        builder.Services.AddSingleton<IWebhookDeliveryLock>(new StripedWebhookDeliveryLock(stripeCount));
        builder.AddMiddleware<PartitionedDeliveryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures serialized outbound deliveries using a custom delivery lock type (e.g. Postgres advisory lock, Redis distributed lock).
    /// </summary>
    /// <typeparam name="TLock">The type implementing <see cref="IWebhookDeliveryLock"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UsePartitionedDelivery<TLock>(this IWebhookBuilder builder) where TLock : class, IWebhookDeliveryLock {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookDeliveryLock>();
        builder.Services.AddSingleton<IWebhookDeliveryLock, TLock>();
        builder.AddMiddleware<PartitionedDeliveryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures serialized outbound deliveries using a delivery lock resolved via a factory delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="implementationFactory">The factory delegate used to resolve the <see cref="IWebhookDeliveryLock"/> instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="implementationFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UsePartitionedDelivery(this IWebhookBuilder builder, Func<IServiceProvider, IWebhookDeliveryLock> implementationFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(implementationFactory);
        builder.Services.RemoveAll<IWebhookDeliveryLock>();
        builder.Services.AddSingleton(implementationFactory);
        builder.AddMiddleware<PartitionedDeliveryMiddleware>();
        return builder;
    }
}