namespace Wiaoj.Webhooks.Concurrency;

/// <summary>
/// Configuration options for partitioned delivery concurrency and FIFO message serialization.
/// </summary>
public sealed class PartitionedDeliveryOptions {
    /// <summary>Default number of power-of-two lock stripes (4096).</summary>
    public const int DefaultStripes = 4096;

    /// <summary>Gets or sets the number of lock stripes. Must be a power of two.</summary>
    public int Stripes { get; set; } = DefaultStripes;

    /// <summary>
    /// Gets or sets a custom strategy to derive the partition key from the delivery context.
    /// Defaults to <c>ctx.PartitionKey</c> (which falls back to <c>ctx.Endpoint.Id.Value</c>).
    /// </summary>
    public Func<WebhookDeliveryContext, string> PartitionKeySelector { get; set; } = static ctx => ctx.PartitionKey;
}