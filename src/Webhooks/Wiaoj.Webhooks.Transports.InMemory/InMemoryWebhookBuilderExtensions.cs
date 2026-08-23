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
    /// Configures high-performance partition-sharded in-memory transports ensuring lock-free FIFO message ordering per partition.
    /// </summary>
    public static IWebhookBuilder UseShardedInMemoryTransport(
        this IWebhookBuilder builder,
        int shardCount = 8,
        int? capacityPerShard = null) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfLessThan(shardCount, 1);

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