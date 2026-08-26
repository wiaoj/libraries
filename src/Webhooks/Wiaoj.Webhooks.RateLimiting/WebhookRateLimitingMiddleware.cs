using Microsoft.Extensions.Logging;
using Wiaoj.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting;
using Wiaoj.Webhooks.RateLimiting.Diagnostics;

namespace Wiaoj.Webhooks.RateLimiting;

/// <summary>
/// Webhook delivery middleware that enforces rate limiting policies via <see cref="IRateLimiter"/>
/// and re-enqueues throttled deliveries with calculated backoff delays.
/// </summary>
internal sealed class WebhookRateLimitingMiddleware : IWebhookMiddleware {
    private readonly IRateLimiter _rateLimiter;
    private readonly WebhookRateLimitingOptions _options;
    private readonly ILogger<WebhookRateLimitingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookRateLimitingMiddleware"/> class.
    /// </summary>
    public WebhookRateLimitingMiddleware(
        IRateLimiter rateLimiter,
        WebhookRateLimitingOptions options,
        ILogger<WebhookRateLimitingMiddleware> logger) {
        Preca.ThrowIfNull(rateLimiter);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._rateLimiter = rateLimiter;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string key = this._options.KeySelector(context);
        int cost = this._options.CostResolver(context);

        RateLimitDecision decision = this._options.PolicyName is not null
            ? await this._rateLimiter.TryAcquireAsync(this._options.PolicyName, key, cost, cancellationToken).ConfigureAwait(false)
            : await this._rateLimiter.TryAcquireAsync(key, cost, cancellationToken).ConfigureAwait(false);

        if(!decision.IsAllowed) {
            TimeSpan retryAfter = decision.RetryAfter.ToPositiveOrDefault(1.Seconds());

            this._logger.LogRateLimitExceeded(context.Endpoint.Id.Value, retryAfter.TotalMilliseconds);
            context.SetResult(WebhookDeliveryResult.RateLimited(context.Endpoint.Id.Value, retryAfter));
            return;
        }

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}