using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Runs a single webhook delivery attempt through the configured <see cref="IWebhookMiddleware"/>
/// chain and terminates it at the configured <see cref="IWebhookDeliverer"/>.
/// </summary>
/// <remarks>
/// Middleware are composed in the order provided: the first middleware runs first and wraps
/// everything after it, exactly like ASP.NET Core's own middleware pipeline. If a middleware
/// does not invoke its <see cref="WebhookDelegate"/> parameter, the chain short-circuits and
/// the deliverer is never called — <see cref="RunAsync"/> still returns a
/// <see cref="WebhookDeliveryAttempt"/>, but the underlying <see cref="WebhookDeliveryResult"/>
/// reflects whatever the last middleware to touch <see cref="WebhookDeliveryContext.Items"/> recorded.
/// </remarks>
internal sealed class WebhookPipelineRunner {
    private readonly WebhookDelegate _pipeline;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookPipelineRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPipelineRunner"/> class.
    /// </summary>
    /// <param name="middleware">The middleware chain, in execution order.</param>
    /// <param name="deliverer">The terminal deliverer invoked after all middleware has run.</param>
    /// <param name="timeProvider">The time provider used for measuring pipeline execution duration and recording attempt timestamps.</param>
    /// <param name="logger">The logger instance.</param>
    public WebhookPipelineRunner(
        IReadOnlyList<IWebhookMiddleware> middleware,
        IWebhookDeliverer deliverer,
        TimeProvider timeProvider,
        ILogger<WebhookPipelineRunner> logger) {
        Preca.ThrowIfNull(middleware);
        Preca.ThrowIfNull(deliverer);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._timeProvider = timeProvider;
        this._logger = logger;
        this._pipeline = BuildPipeline(middleware, deliverer);
    }

    /// <summary>
    /// Runs one delivery attempt for <paramref name="context"/> through the pipeline.
    /// </summary>
    /// <param name="context">The delivery context to run.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>
    /// A <see cref="WebhookDeliveryAttempt"/> describing the outcome. If the pipeline
    /// short-circuited before reaching the deliverer, the result reflects a non-delivery.
    /// </returns>
    public async Task<WebhookDeliveryAttempt> RunAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        int attemptNumber = context.AttemptHistory.Count + 1; 

        this._logger.LogDeliveryAttemptStarting(attemptNumber, context.Endpoint.Id, context.TargetUrl);

        using Activity? activity = WebhookActivitySource.StartDeliveryActivity(context, attemptNumber);

        long startTimestamp = Stopwatch.GetTimestamp();
        await this._pipeline(context, cancellationToken);
        TimeSpan duration = Stopwatch.GetElapsedTime(startTimestamp);
        double durationMs = duration.TotalMilliseconds;

        WebhookDeliveryResult result = context.Items.TryGetValue(WebhookDeliveryContextItemKeys.Result, out object? stored)
            && stored is WebhookDeliveryResult capturedResult
                ? capturedResult
                : WebhookDeliveryResult.Permanent("Pipeline short-circuited before reaching a deliverer.", PermanentFailureReason.General);
         
        int? statusCode = result switch {
            WebhookDeliveryResult.Delivered d => d.StatusCode,
            WebhookDeliveryResult.TransientFailure tf => tf.StatusCode,
            WebhookDeliveryResult.PermanentFailure pf => pf.StatusCode,
            _ => null
        };

        string? errorMessage = result switch {
            WebhookDeliveryResult.TransientFailure tf => tf.ErrorMessage,
            WebhookDeliveryResult.PermanentFailure pf => pf.ErrorMessage,
            _ => null
        };

        TagList tags = new() {
            { "webhook.endpoint_id", context.Endpoint.Id.Value },
            { "webhook.success", result.IsSuccess },
            { "webhook.attempt_number", attemptNumber }
        };
        if(statusCode.HasValue) {
            tags.Add("webhook.status_code", statusCode.Value);
        }

        WebhookMeter.DeliveryAttemptCount.Add(1, tags);
        WebhookMeter.DeliveryDuration.Record(durationMs, tags);

        if(result.IsSuccess) {
            WebhookMeter.DeliverySuccessCount.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else {
            WebhookMeter.DeliveryFailureCount.Add(1, tags);
            activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
        }

        activity?.SetTag("webhook.success", result.IsSuccess);
        activity?.SetTag("webhook.status_code", statusCode);
        activity?.SetTag("webhook.duration_ms", durationMs);

        return new WebhookDeliveryAttempt(context.Endpoint.Id, attemptNumber, _timeProvider.GetUnixTimestamp(), duration, result);
    }

    private static WebhookDelegate BuildPipeline(IReadOnlyList<IWebhookMiddleware> middleware, IWebhookDeliverer deliverer) {
        WebhookDelegate terminal = async (ctx, ct) => {
            WebhookDeliveryResult result = await deliverer.DeliverAsync(ctx, ct);
            ctx.Items[WebhookDeliveryContextItemKeys.Result] = result;
        };

        WebhookDelegate pipeline = terminal;

        for(int i = middleware.Count - 1; i >= 0; i--) {
            IWebhookMiddleware current = middleware[i];
            WebhookDelegate next = pipeline;
            pipeline = (ctx, ct) => current.InvokeAsync(ctx, next, ct);
        }

        return pipeline;
    }
}