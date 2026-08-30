using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks.BloomFilter.Diagnostics;

namespace Wiaoj.Webhooks.BloomFilter;

/// <summary>
/// Webhook delivery middleware that prevents duplicate event deliveries using a high-performance <see cref="IBloomFilter"/>.
/// </summary>
internal sealed class BloomFilterDeduplicationMiddleware : IWebhookMiddleware {
    private readonly IBloomFilter _bloomFilter;
    private readonly BloomFilterDeduplicationOptions _options;
    private readonly ILogger<BloomFilterDeduplicationMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilterDeduplicationMiddleware"/> class.
    /// </summary>
    /// <param name="bloomFilter">The underlying Bloom filter instance.</param>
    /// <param name="options">The deduplication options.</param>
    /// <param name="logger">The logger instance.</param>
    public BloomFilterDeduplicationMiddleware(
        IBloomFilter bloomFilter,
        BloomFilterDeduplicationOptions options,
        ILogger<BloomFilterDeduplicationMiddleware> logger) {
        Preca.ThrowIfNull(bloomFilter);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        options.Validate();
        this._bloomFilter = bloomFilter;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string key = this._options.KeySelector(context);
         
        if(this._bloomFilter.Contains(key.AsSpan())) {
            this._logger.LogDuplicateEventSkipped(context.Endpoint.Id, key);

            System.Diagnostics.Activity? activity = System.Diagnostics.Activity.Current;
            if(activity is not null) {
                activity.SetTag("webhook.deduplicated", true);
                activity.AddEvent(new System.Diagnostics.ActivityEvent("webhook.bloom_filter.hit", tags: new System.Diagnostics.ActivityTagsCollection {
                    { "webhook.idempotency_key", key },
                    { "webhook.endpoint_id", context.Endpoint.Id.Value }
                }));
            }

            System.Diagnostics.TagList tags = new() {
                { "webhook.endpoint_id", context.Endpoint.Id.Value }
            };
            BloomFilterMeter.BloomFilterHitsCount.Add(1, tags);

            context.SetResult(WebhookDeliveryResult.Duplicate(key));
            return;
        }
         
        await next(context, cancellationToken).ConfigureAwait(false);
         
        if(context.HasSuccessResult()) {
            this._bloomFilter.Add(key.AsSpan());
        }
    }
}