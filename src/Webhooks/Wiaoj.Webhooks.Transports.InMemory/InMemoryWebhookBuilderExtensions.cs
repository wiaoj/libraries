using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Transports.InMemory;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring in-memory standalone and sharded webhook transports on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class InMemoryWebhookBuilderExtensions {
    /// <summary>
    /// Configures the native in-memory webhook transport with default settings.
    /// </summary>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder) {
        return UseInMemoryTransport(builder, new InMemoryWebhookTransportOptions());
    }

    /// <summary>
    /// Configures the native in-memory webhook transport with a bounded channel capacity.
    /// </summary>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder, int capacity) {
        Preca.ThrowIfLessThan(capacity, 1);
        return UseInMemoryTransport(builder, new InMemoryWebhookTransportOptions { Capacity = capacity });
    }

    /// <summary>
    /// Configures the native in-memory webhook transport using a configuration delegate.
    /// </summary>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder, Action<InMemoryWebhookTransportOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        InMemoryWebhookTransportOptions options = new();
        configure(options);
        return UseInMemoryTransport(builder, options);
    }

    /// <summary>
    /// Configures the native in-memory webhook transport with the specified options.
    /// </summary>
    public static IWebhookBuilder UseInMemoryTransport(this IWebhookBuilder builder, InMemoryWebhookTransportOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        builder.Services.RemoveAll<IWebhookTransport>();
        builder.Services.AddSingleton(Options.Create(options));
        builder.Services.AddSingleton<IWebhookTransport>(sp => new InMemoryWebhookTransport(options, sp.GetRequiredService<ILogger<InMemoryWebhookTransport>>()));
        builder.Services.AddHostedService<InMemoryWebhookConsumer>();

        return builder;
    }

    /// <summary>
    /// Configures high-performance partition-sharded in-memory transports using default 8 shards and unbounded capacity.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseShardedInMemoryTransport(this IWebhookBuilder builder) {
        return UseShardedInMemoryTransport(builder, shardCount: 8, capacityPerShard: null);
    }

    /// <summary>
    /// Configures high-performance partition-sharded in-memory transports with a specified shard count and unbounded capacity.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="shardCount">The number of concurrent parallel in-memory transport shards.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="shardCount"/> is less than 1.</exception>
    public static IWebhookBuilder UseShardedInMemoryTransport(this IWebhookBuilder builder, int shardCount) {
        return UseShardedInMemoryTransport(builder, shardCount, capacityPerShard: null);
    }

    /// <summary>
    /// Configures high-performance partition-sharded in-memory transports with a specified shard count and bounded shard capacity.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="shardCount">The number of concurrent parallel in-memory transport shards.</param>
    /// <param name="capacityPerShard">The maximum bounded queue capacity per shard.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="shardCount"/> is less than 1 or <paramref name="capacityPerShard"/> is non-positive.</exception>
    public static IWebhookBuilder UseShardedInMemoryTransport(
        this IWebhookBuilder builder,
        int shardCount,
        int? capacityPerShard) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfLessThan(shardCount, 1);
        if(capacityPerShard.HasValue) {
            Preca.ThrowIfLessThan(capacityPerShard.Value, 1);
        }

        builder.Services.RemoveAll<IWebhookTransport>();
        builder.Services.AddSingleton<IWebhookTransport>(sp => {
            ILogger<InMemoryWebhookTransport> logger = sp.GetRequiredService<ILogger<InMemoryWebhookTransport>>();
            InMemoryWebhookTransport[] shards = new InMemoryWebhookTransport[shardCount];

            for(int i = 0; i < shardCount; i++) {
                InMemoryWebhookTransportOptions shardOpts = new() { Capacity = capacityPerShard, Concurrency = 1 };
                shards[i] = new InMemoryWebhookTransport(shardOpts, logger);
            }

            return new ShardedWebhookTransport(shards);
        });

        builder.Services.AddHostedService<InMemoryWebhookConsumer>();
        return builder;
    }
}