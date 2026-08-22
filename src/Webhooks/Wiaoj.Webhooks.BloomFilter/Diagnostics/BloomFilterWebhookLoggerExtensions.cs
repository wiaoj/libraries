using Microsoft.Extensions.Logging;

namespace Wiaoj.Webhooks.BloomFilter.Diagnostics;

/// <summary>
/// Structured high-performance logging extension methods for the Bloom Filter Webhook deduplication middleware.
/// Uses [LoggerMessage] source generator.
/// </summary>
public static partial class BloomFilterWebhookLoggerExtensions {

    /// <summary>Logs that a duplicate webhook event was suppressed by the Bloom Filter.</summary>
    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Information, 
        Message = "Duplicate webhook event detected for endpoint '{EndpointId}' with deduplication key '{DeduplicationKey}'. Delivery skipped.")]
    public static partial void LogDuplicateEventSkipped(this ILogger logger, WebhookEndpointId endpointId, string deduplicationKey);
}
