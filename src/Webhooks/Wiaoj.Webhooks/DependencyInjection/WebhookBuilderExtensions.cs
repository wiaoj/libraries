using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Signing;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Core extension methods for <see cref="IWebhookBuilder"/>.
/// </summary>
public static class WebhookBuilderExtensions {
    /// <summary>
    /// Configures the default in-memory webhook store.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseInMemoryStore(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton<IWebhookStore, InMemoryWebhookStore>();
        return builder;
    }

    /// <summary>
    /// Configures a custom webhook store implementation.
    /// </summary>
    /// <typeparam name="TStore">The type of the webhook store.</typeparam>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseStore<TStore>(this IWebhookBuilder builder) where TStore : class, IWebhookStore {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton<IWebhookStore, TStore>();
        return builder;
    }

    /// <summary>
    /// Configures a specific singleton instance of the webhook store.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="store">The webhook store instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseStore(this IWebhookBuilder builder, IWebhookStore store) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(store);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton(store);
        return builder;
    }

    /// <summary>
    /// Disables persistent job auditing by using <see cref="NullWebhookStore"/>.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseNullStore(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookStore>();
        builder.Services.AddSingleton<IWebhookStore>(NullWebhookStore.Instance);
        return builder;
    }

    /// <summary>
    /// Configures a custom endpoint resolver implementation.
    /// </summary>
    /// <typeparam name="TResolver">The type of the endpoint resolver.</typeparam>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseEndpointResolver<TResolver>(this IWebhookBuilder builder) where TResolver : class, IWebhookEndpointResolver {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton<IWebhookEndpointResolver, TResolver>();
        return builder;
    }

    /// <summary>
    /// Configures a specific singleton instance of the endpoint resolver.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="resolver">The endpoint resolver instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseEndpointResolver(this IWebhookBuilder builder, IWebhookEndpointResolver resolver) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolver);
        builder.Services.RemoveAll<IWebhookEndpointResolver>();
        builder.Services.AddSingleton(resolver);
        return builder;
    }

    /// <summary>
    /// Configures HMAC-SHA256 (scheme "v1") cryptographic signing and registers <see cref="SigningMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseHmacSha256Signing(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner, HmacSha256WebhookSigner>();
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures HMAC-SHA512 (scheme "v2") cryptographic signing and registers <see cref="SigningMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseHmacSha512Signing(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner, HmacSha512WebhookSigner>();
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures a custom signer implementation and registers <see cref="SigningMiddleware"/> in the pipeline.
    /// </summary>
    /// <typeparam name="TSigner">The type of the webhook signer.</typeparam>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseSigner<TSigner>(this IWebhookBuilder builder) where TSigner : class, IWebhookSigner {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSigner>();
        builder.Services.AddSingleton<IWebhookSigner, TSigner>();
        builder.AddMiddleware<SigningMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures exponential backoff with full-jitter retry strategy and registers <see cref="RetryMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseExponentialBackoffRetry(this IWebhookBuilder builder) {
        return UseExponentialBackoffRetry(builder, new ExponentialBackoffOptions());
    }

    /// <summary>
    /// Configures exponential backoff with specified options and registers <see cref="RetryMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="options">The exponential backoff options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseExponentialBackoffRetry(this IWebhookBuilder builder, ExponentialBackoffOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy>(new ExponentialBackoffPolicy(options));
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures linear backoff retry strategy and registers <see cref="RetryMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="maxAttempts">The maximum total number of delivery attempts.</param>
    /// <param name="initialDelay">The delay before the first retry.</param>
    /// <param name="step">The additional duration added to each subsequent retry delay.</param>
    /// <param name="maxDelay">The maximum delay cap.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseLinearBackoffRetry(this IWebhookBuilder builder, int maxAttempts, TimeSpan initialDelay, TimeSpan step, TimeSpan maxDelay) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy>(new LinearBackoffPolicy(maxAttempts, initialDelay, step, maxDelay));
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures fixed interval retry strategy and registers <see cref="RetryMiddleware"/> in the pipeline.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="maxAttempts">The maximum total number of delivery attempts.</param>
    /// <param name="interval">The constant delay interval between retry attempts.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseFixedIntervalRetry(this IWebhookBuilder builder, int maxAttempts, TimeSpan interval) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookRetryPolicy>();
        builder.Services.AddSingleton<IWebhookRetryPolicy>(new FixedIntervalBackoffPolicy(maxAttempts, interval));
        builder.AddMiddleware<RetryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures serialized outbound deliveries per <see cref="WebhookEndpointId"/> using the default 4096-stripe in-memory lock.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UsePartitionedDelivery(this IWebhookBuilder builder) {
        return UsePartitionedDelivery(builder, 4096);
    }

    /// <summary>
    /// Configures serialized outbound deliveries per <see cref="WebhookEndpointId"/> with the specified number of in-memory lock stripes.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="stripes">The number of lock partitions. Must be a positive power of two (e.g., 1024, 2048, 4096).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UsePartitionedDelivery(this IWebhookBuilder builder, int stripes) {
        Preca.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<IWebhookDeliveryLock>(new StripedWebhookDeliveryLock(stripes));
        builder.AddMiddleware<PartitionedDeliveryMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures serialized outbound deliveries using a custom or distributed <see cref="IWebhookDeliveryLock"/> (e.g. Redis, PostgreSQL advisory locks).
    /// </summary>
    /// <typeparam name="TLock">The type of the custom delivery lock implementation.</typeparam>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UsePartitionedDelivery<TLock>(this IWebhookBuilder builder) where TLock : class, IWebhookDeliveryLock {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookDeliveryLock>();
        builder.Services.AddSingleton<IWebhookDeliveryLock, TLock>();
        builder.AddMiddleware<PartitionedDeliveryMiddleware>();
        return builder;
    }
}