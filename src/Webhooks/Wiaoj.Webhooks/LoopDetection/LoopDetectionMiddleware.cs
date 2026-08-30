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
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        // 1. Evaluate Integer Hop Limit
        int rawHops = context.GetHeader<int>(this._options.HopCountHeaderName, defaultValue: 0);
        int currentHops = Math.Max(0, rawHops);
        if(currentHops >= this._options.MaxHops) {
            string reason = $"Exceeded maximum allowable hop count of {this._options.MaxHops} (Current: {currentHops}).";
            this.HandleLoopDetected(context, reason, currentHops);
            return;
        }

        // 2. Evaluate Causal Execution Graph Cycle
        if(this._options.TrackCausalChain) {
            string? existingChain = context.GetHeader(this._options.CausalChainHeaderName);
            if(!string.IsNullOrWhiteSpace(existingChain) && ContainsNode(existingChain, this._options.InstanceId)) {
                string reason = $"Cycle detected: instance '{this._options.InstanceId}' already present in chain '{existingChain}'.";
                this.HandleLoopDetected(context, reason, currentHops);
                return;
            }
        }

        // 3. Mutate Outbound Headers for Downstream
        int nextHop = currentHops >= int.MaxValue ? int.MaxValue : currentHops + 1;
        context.SetHeader(this._options.HopCountHeaderName, nextHop.ToString());

        if(this._options.TrackCausalChain) {
            context.AppendHeader(this._options.CausalChainHeaderName, this._options.InstanceId);
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
                { "webhook.instance_id", this._options.InstanceId }
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

    private static bool ContainsNode(string chain, string instanceId) {
        ReadOnlySpan<char> chainSpan = chain.AsSpan();
        while(!chainSpan.IsEmpty) {
            int commaIndex = chainSpan.IndexOf(',');
            ReadOnlySpan<char> token = commaIndex >= 0 ? chainSpan[..commaIndex] : chainSpan;
            token = token.Trim().Trim('"');

            if(token.Equals(instanceId.AsSpan(), StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if(commaIndex < 0) {
                break;
            }

            chainSpan = chainSpan[(commaIndex + 1)..];
        }

        return false;
    }
}
