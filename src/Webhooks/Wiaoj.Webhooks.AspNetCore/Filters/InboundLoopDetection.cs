using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Wiaoj.Webhooks.AspNetCore.Diagnostics;

namespace Wiaoj.Webhooks.AspNetCore.Filters;

/// <summary>
/// Inbound half of loop detection, shared by the receiver and hub filters: reads the hop count and causal chain of a
/// request, rejects it when it has gone too far, and otherwise hands the causality to the handler's
/// <see cref="WebhookCausalityScope"/>.
/// </summary>
internal static class InboundLoopDetection {
    /// <summary>
    /// Reads the causality an inbound request carries. A hop header folded by a proxy (<c>"1, 3"</c>) counts as its
    /// largest value; values that are not non-negative integers are ignored.
    /// </summary>
    public static WebhookCausality Capture(HttpRequest request, WebhookReceiverPolicy policy) {
        int hops = ParseMaxHopCount(request.Headers[policy.HopCountHeaderName].ToString());
        string chain = request.Headers[policy.CausalChainHeaderName].ToString();
        return new WebhookCausality(hops, chain);
    }

    /// <summary>
    /// Returns the response rejecting the request, or <see langword="null"/> when it may be handled. A request is rejected
    /// when its hop count exceeds <see cref="WebhookReceiverPolicy.MaxHops"/>, or when <see cref="WebhookReceiverPolicy.InstanceId"/>
    /// is set and already appears in its causal chain.
    /// </summary>
    public static IResult? Check(WebhookCausality causality, WebhookReceiverPolicy policy, HttpRequest request, in TagList telemetryTags, Activity? activity) {
        // MaxHops is the largest hop count a delivery may carry: a sender with the same limit sends hop MaxHops, and the
        // receiver must accept it.
        if(causality.HopCount > policy.MaxHops) {
            WebhookInboundMeter.LoopDetectedCount.Add(1, telemetryTags);
            activity?.SetStatus(ActivityStatusCode.Error, "Inbound hop limit exceeded");
            return WebhookReceiverResponses.LoopDetected(policy.MaxHops, causality.HopCount, request.Path);
        }

        if(!string.IsNullOrWhiteSpace(policy.InstanceId) && causality.Contains(policy.InstanceId)) {
            WebhookInboundMeter.LoopDetectedCount.Add(1, telemetryTags);
            activity?.SetStatus(ActivityStatusCode.Error, "Inbound causal cycle detected");
            return WebhookReceiverResponses.LoopDetected(policy.MaxHops, causality.HopCount, request.Path);
        }

        return null;
    }

    private static int ParseMaxHopCount(string rawHeader) {
        ReadOnlySpan<char> span = rawHeader.AsSpan();
        int maxParsed = 0;
        while(!span.IsEmpty) {
            int commaIndex = span.IndexOf(',');
            ReadOnlySpan<char> token = commaIndex >= 0 ? span[..commaIndex] : span;
            token = token.Trim();

            if(!token.IsEmpty && int.TryParse(token, out int parsed) && parsed > maxParsed) {
                maxParsed = parsed;
            }

            if(commaIndex < 0) {
                break;
            }

            span = span[(commaIndex + 1)..];
        }
        return maxParsed;
    }
}
