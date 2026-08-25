using Microsoft.Extensions.Logging;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting;
using Wiaoj.Webhooks.RateLimiting.Diagnostics;

namespace Wiaoj.Webhooks.RateLimiting;

/// <summary>
/// Webhook delivery middleware that enforces per-endpoint rate limiting
/// using an <see cref="IRateLimitAlgorithm"/> and re-enqueues throttled deliveries.
/// </summary>
internal sealed class WebhookRateLimitingMiddleware : IWebhookMiddleware {
    private readonly IRateLimitAlgorithm _algorithm;
    private readonly WebhookRateLimitingOptions _options;
    private readonly ILogger<WebhookRateLimitingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookRateLimitingMiddleware"/> class.
    /// </summary>
    public WebhookRateLimitingMiddleware(
        IRateLimitAlgorithm algorithm,
        WebhookRateLimitingOptions options,
        ILogger<WebhookRateLimitingMiddleware> logger) {
        Preca.ThrowIfNull(algorithm);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._algorithm = algorithm;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string key = this._options.KeySelector(context);
        int cost = this._options.CostResolver(context);

        RateLimitDecision decision = await this._algorithm
            .TryAcquireAsync(key, cost, cancellationToken)
            .ConfigureAwait(false);

        if(!decision.IsAllowed) {
            TimeSpan retryAfter = decision.RetryAfter is { } ra && ra > TimeSpan.Zero
               ? ra
               : TimeSpan.FromSeconds(1);

            this._logger.LogRateLimitExceeded(context.Endpoint.Id.Value, retryAfter.TotalMilliseconds);
             
            context.SetResult(WebhookDeliveryResult.RateLimited(context.Endpoint.Id.Value, retryAfter));
            return;
        }

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}