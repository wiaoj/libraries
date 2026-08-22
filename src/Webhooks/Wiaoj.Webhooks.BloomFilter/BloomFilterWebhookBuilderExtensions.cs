using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks.BloomFilter;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering BloomFilter-based deduplication in the Webhooks builder.
/// </summary>
public static class BloomFilterWebhookBuilderExtensions {

    // ────────────────────────────────────────────────────────────────────────
    // 1. KEYED FILTER RESOLUTION (from DI by name)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures deduplication using a keyed <see cref="IBloomFilter"/> and specified options.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        string filterName,
        BloomFilterDeduplicationOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(filterName);
        Preca.ThrowIfNull(options);

        options.Validate();

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<BloomFilterDeduplicationMiddleware>(sp => new BloomFilterDeduplicationMiddleware(
            sp.GetRequiredKeyedService<IBloomFilter>(filterName),
            options,
            sp.GetRequiredService<ILogger<BloomFilterDeduplicationMiddleware>>()));

        return builder;
    }

    /// <summary>
    /// Configures deduplication using a keyed <see cref="IBloomFilter"/> with default options.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        string filterName) {
        return UseBloomFilterDeduplication(builder, filterName, new BloomFilterDeduplicationOptions());
    }

    /// <summary>
    /// Configures deduplication using a keyed <see cref="IBloomFilter"/> and options configure delegate.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        string filterName,
        Action<BloomFilterDeduplicationOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(filterName);
        Preca.ThrowIfNull(configure);

        BloomFilterDeduplicationOptions options = new();
        configure(options);
        return UseBloomFilterDeduplication(builder, filterName, options);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. DIRECT INSTANCE (passed directly)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures deduplication using an existing <see cref="IBloomFilter"/> instance and specified options.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        IBloomFilter bloomFilter,
        BloomFilterDeduplicationOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(bloomFilter);
        Preca.ThrowIfNull(options);

        options.Validate();

        builder.Services.AddSingleton(bloomFilter);
        builder.Services.AddSingleton(options);
        builder.AddMiddleware<BloomFilterDeduplicationMiddleware>(sp => new BloomFilterDeduplicationMiddleware(
            bloomFilter,
            options,
            sp.GetRequiredService<ILogger<BloomFilterDeduplicationMiddleware>>()));

        return builder;
    }

    /// <summary>
    /// Configures deduplication using an existing <see cref="IBloomFilter"/> instance with default options.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        IBloomFilter bloomFilter) {
        return UseBloomFilterDeduplication(builder, bloomFilter, new BloomFilterDeduplicationOptions());
    }

    /// <summary>
    /// Configures deduplication using an existing <see cref="IBloomFilter"/> instance and options configure delegate.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        IBloomFilter bloomFilter,
        Action<BloomFilterDeduplicationOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(bloomFilter);
        Preca.ThrowIfNull(configure);

        BloomFilterDeduplicationOptions options = new();
        configure(options);
        return UseBloomFilterDeduplication(builder, bloomFilter, options);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. DEFAULT DI RESOLUTION (unnamed IBloomFilter from container)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures deduplication resolving <see cref="IBloomFilter"/> from the DI container with specified options.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        BloomFilterDeduplicationOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        options.Validate();

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<BloomFilterDeduplicationMiddleware>();

        return builder;
    }

    /// <summary>
    /// Configures deduplication resolving <see cref="IBloomFilter"/> from the DI container with default options.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(this IWebhookBuilder builder) {
        return UseBloomFilterDeduplication(builder, new BloomFilterDeduplicationOptions());
    }

    /// <summary>
    /// Configures deduplication resolving <see cref="IBloomFilter"/> from the DI container and options configure delegate.
    /// </summary>
    public static IWebhookBuilder UseBloomFilterDeduplication(
        this IWebhookBuilder builder,
        Action<BloomFilterDeduplicationOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        BloomFilterDeduplicationOptions options = new();
        configure(options);
        return UseBloomFilterDeduplication(builder, options);
    }
}