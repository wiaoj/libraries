using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Idempotency;

/// <summary>
/// Webhook delivery middleware that prevents duplicate deliveries using an <see cref="IIdempotencyStore"/> and <see cref="IIdempotencyKeyGenerator"/>.
/// </summary>
public sealed class IdempotencyMiddleware : IWebhookMiddleware {
    private readonly IIdempotencyStore _store;
    private readonly IIdempotencyKeyGenerator _keyGenerator;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyMiddleware"/> class.
    /// </summary>
    /// <param name="store">The persistent or in-memory idempotency store.</param>
    /// <param name="keyGenerator">The strategy used to generate deterministic idempotency keys.</param>
    /// <param name="options">The idempotency configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public IdempotencyMiddleware(
        IIdempotencyStore store,
        IIdempotencyKeyGenerator keyGenerator,
        IdempotencyOptions options,
        ILogger<IdempotencyMiddleware> logger) {
        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(keyGenerator);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        options.Validate();
        this._store = store;
        this._keyGenerator = keyGenerator;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        IdempotencyKey key = this._options.CustomKeySelector is not null
            ? this._options.CustomKeySelector(context)
            : this._keyGenerator.GenerateKey(context);

        context.SetIdempotencyKey(key);
        Activity.Current?.SetTag("webhook.idempotency_key", key.Value);

        bool shouldCheckDeduplication = !(this._options.BypassOnReplay && context.IsReplay());

        // 1. Atomically reserve the key *before* running the downstream pipeline.
        //    This closes the check-then-act race that existed when ContainsAsync (check)
        //    and MarkProcessedAsync (commit) were two separate, non-atomic steps: two
        //    concurrent deliveries carrying the same key could both observe "not yet
        //    processed" and both reach the downstream pipeline. TryMarkProcessedAsync
        //    performs the check-and-reserve as a single atomic operation on the store.
        if(shouldCheckDeduplication) {
            bool reserved = await this._store.TryMarkProcessedAsync(key, this._options.Window, cancellationToken).ConfigureAwait(false);
            if(!reserved) {
                this._logger.LogPipelineShortCircuited(context.Endpoint.Id, $"Duplicate event intercepted by IdempotencyStore with key '{key.Value}'.");
                
                Activity? activity = Activity.Current;
                if(activity is not null) {
                    activity.SetTag("webhook.deduplicated", true);
                    activity.AddEvent(new ActivityEvent("webhook.deduplicated", tags: new ActivityTagsCollection {
                        { "webhook.idempotency_key", key.Value },
                        { "webhook.endpoint_id", context.Endpoint.Id.Value }
                    }));
                }

                TagList tags = new() {
                    { "webhook.endpoint_id", context.Endpoint.Id.Value },
                    { "webhook.source", "IdempotencyStore" }
                };
                WebhookMeter.DeduplicatedCount.Add(1, tags);

                context.SetResult(WebhookDeliveryResult.Duplicate(key.Value));
                return;
            }
        }

        // 2. Execute downstream pipeline
        await next(context, cancellationToken).ConfigureAwait(false);

        // 3. If the delivery did not succeed, release the reservation so a legitimate
        //    retry for the same key is not rejected as a false-positive duplicate.
        if(shouldCheckDeduplication && !context.HasSuccessResult()) {
            await this._store.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }
}