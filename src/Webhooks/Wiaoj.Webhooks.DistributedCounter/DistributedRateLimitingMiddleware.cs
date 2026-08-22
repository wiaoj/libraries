using Microsoft.Extensions.Logging;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks.DistributedCounter.Diagnostics;

namespace Wiaoj.Webhooks.DistributedCounter;

/// <summary>
/// Webhook delivery middleware that enforces per-endpoint distributed rate limiting
/// using an <see cref="IDistributedCounterFactory"/> and re-enqueues throttled deliveries.
/// </summary>
public sealed class DistributedRateLimitingMiddleware : IWebhookMiddleware {
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly DistributedRateLimitingOptions _options; 
    private readonly ILogger<DistributedRateLimitingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRateLimitingMiddleware"/> class.
    /// </summary>
    /// <param name="counterFactory">The distributed counter factory.</param>
    /// <param name="options">The rate limiting options.</param> 
    /// <param name="logger">The logger instance.</param>
    public DistributedRateLimitingMiddleware(
        IDistributedCounterFactory counterFactory,
        DistributedRateLimitingOptions options, 
        ILogger<DistributedRateLimitingMiddleware> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNull(options); 
        Preca.ThrowIfNull(logger);

        options.Validate();

        this._counterFactory = counterFactory;
        this._options = options; 
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string key = this._options.KeySelector(context);
        IDistributedCounter counter = this._counterFactory.Create(key);

        CounterExpiry expiry = CounterExpiry.From(this._options.Window);
        CounterLimitResult limitResult = await counter.TryIncrementAsync(1,
                                                                         this._options.MaxRequestsPerWindow,
                                                                         expiry,
                                                                         cancellationToken).ConfigureAwait(false);

        if(!limitResult.IsAllowed) {
            string endpointId = context.Endpoint.Id.Value;
            this._logger.LogRateLimitExceeded(
                this._options.MaxRequestsPerWindow,
                this._options.Window.TotalMilliseconds,
                endpointId);

            context.SetResult(WebhookDeliveryResult.Transient(
                "Rate limit exceeded. Webhook delivery re-enqueued.",
                statusCode: 429,
                retryAfter: this._options.Window));
            return;
        }

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}