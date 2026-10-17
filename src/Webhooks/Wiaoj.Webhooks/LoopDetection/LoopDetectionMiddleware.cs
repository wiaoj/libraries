using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.LoopDetection;

/// <summary>
/// Pipeline middleware that inspects and enforces hop limits and evaluates causal execution chains
/// to detect and short-circuit infinite webhook cascading storms and cycle loops.
/// </summary>
public sealed class LoopDetectionMiddleware : IWebhookMiddleware {
    private readonly LoopDetectionOptions _options;
    private readonly ILogger<LoopDetectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopDetectionMiddleware"/> class with default options.
    /// </summary>
    public LoopDetectionMiddleware() : this(new LoopDetectionOptions(), NullLogger<LoopDetectionMiddleware>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopDetectionMiddleware"/> class with the specified options and a null logger.
    /// </summary>
    /// <param name="options">The loop detection configuration options.</param>
    public LoopDetectionMiddleware(LoopDetectionOptions options) : this(options, NullLogger<LoopDetectionMiddleware>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopDetectionMiddleware"/> class with the specified options and logger.
    /// </summary>
    /// <param name="options">The loop detection configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public LoopDetectionMiddleware(LoopDetectionOptions options, ILogger<LoopDetectionMiddleware> logger) {
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A delivery starts from the hop count and causal chain its job inherited from the webhook that caused it
    /// (<see cref="WebhookDeliveryContextExtensions.GetCausality"/>), combined with any hop or chain header an earlier
    /// middleware already set. It is dropped when that hop count has reached <see cref="LoopDetectionOptions.MaxHops"/> or
    /// the chain already contains <see cref="LoopDetectionOptions.ServiceName"/>; otherwise it is sent with the hop count
    /// plus one and the chain plus this service.
    /// </remarks>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        if(!this._options.Enabled) {
            await next(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        WebhookCausality inherited = context.GetCausality();

        // 1. Evaluate Integer Hop Limit
        int headerHops = Math.Max(0, context.GetHeader<int>(this._options.HopCountHeaderName, defaultValue: 0));
        int currentHops = Math.Max(inherited.HopCount, headerHops);
        if(currentHops >= this._options.MaxHops) {
            string reason = $"Exceeded maximum allowable hop count of {this._options.MaxHops} (Current: {currentHops}).";
            this.HandleLoopDetected(context, reason, currentHops);
            return;
        }

        // 2. Evaluate Causal Execution Graph Cycle
        string? existingChain = null;
        if(this._options.TrackCausalChain) {
            existingChain = JoinChains(inherited.CausalChain, context.GetHeader(this._options.CausalChainHeaderName));
            if(WebhookCausality.ChainContains(existingChain, this._options.ServiceName)) {
                string reason = $"Cycle detected: service '{this._options.ServiceName}' already present in chain '{existingChain}'.";
                this.HandleLoopDetected(context, reason, currentHops);
                return;
            }
        }

        // 3. Mutate Outbound Headers for Downstream
        int nextHop = currentHops >= int.MaxValue ? int.MaxValue : currentHops + 1;
        context.SetHeader(this._options.HopCountHeaderName, nextHop.ToString());

        if(this._options.TrackCausalChain) {
            context.SetHeader(this._options.CausalChainHeaderName, JoinChains(existingChain, this._options.ServiceName)!);
        }

        Activity.Current?.SetTag("webhook.hop_count", nextHop);

        // 4. Continue Pipeline
        await next(context, cancellationToken).ConfigureAwait(false);
    }

    private void HandleLoopDetected(WebhookDeliveryContext context, string reason, int currentHops) {
        this._logger.LogWebhookLoopDetected(context.JobId, context.Endpoint.Id, reason);

        Activity? activity = Activity.Current;
        if(activity is not null) {
            activity.SetStatus(ActivityStatusCode.Error, reason);
            activity.SetTag("webhook.loop_detected", true);
            activity.SetTag("webhook.hop_count", currentHops);
            activity.AddEvent(new ActivityEvent("webhook.loop_detected", tags: new ActivityTagsCollection {
                { "reason", reason },
                { "webhook.hop_count", currentHops },
                { "webhook.max_hops", this._options.MaxHops },
                { "webhook.service_name", this._options.ServiceName }
            }));
        }

        TagList metricTags = new() {
            { "webhook.endpoint_id", context.Endpoint.Id.Value },
            { "webhook.reason", reason }
        };
        WebhookMeter.LoopDetectedCount.Add(1, metricTags);

        if(this._options.Behavior == LoopDetectedBehavior.ThrowException) {
            throw new WebhookLoopDetectedException(reason);
        }

        context.SetResult(WebhookDeliveryResult.LoopDetected(reason));
    }

    private static string? JoinChains(string? first, string? second) {
        if(string.IsNullOrWhiteSpace(first)) {
            return string.IsNullOrWhiteSpace(second) ? null : second.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? first.Trim() : $"{first.Trim()}, {second.Trim()}";
    }
}
