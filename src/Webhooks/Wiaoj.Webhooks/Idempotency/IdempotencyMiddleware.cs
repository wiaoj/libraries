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

        bool shouldCheckDeduplication = !(this._options.BypassOnReplay && context.IsReplay());

        // 1. Check if the event was already successfully processed within the active window
        if(shouldCheckDeduplication && await this._store.ContainsAsync(key, cancellationToken).ConfigureAwait(false)) {
            this._logger.LogPipelineShortCircuited(context.Endpoint.Id, $"Duplicate event intercepted by IdempotencyStore with key '{key.Value}'.");
            context.SetResult(WebhookDeliveryResult.Duplicate(key.Value));
            return;
        }

        // 2. Execute downstream pipeline
        await next(context, cancellationToken).ConfigureAwait(false);

        // 3. Atomically commit the idempotency key only upon confirmed successful delivery
        if(context.HasSuccessResult()) {
            await this._store.MarkProcessedAsync(key, this._options.Window, cancellationToken).ConfigureAwait(false);
        }
    }
}