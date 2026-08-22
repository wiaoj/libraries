using Wiaoj.Preconditions;

namespace Wiaoj.Webhooks.BloomFilter;

/// <summary>
/// Configuration options for BloomFilter-based webhook deduplication.
/// </summary>
public sealed class BloomFilterDeduplicationOptions {
    /// <summary>
    /// The default expected capacity of unique events stored in the Bloom filter.
    /// </summary>
    public const long DefaultCapacity = 1_000_000;

    /// <summary>
    /// The default acceptable false positive probability rate (0.1%).
    /// </summary>
    public const double DefaultErrorRate = 0.001;

    /// <summary>
    /// Gets or sets the expected number of unique events. Default is 1,000,000.
    /// </summary>
    public long Capacity { get; set; } = DefaultCapacity;

    /// <summary>
    /// Gets or sets the desired false positive probability. Default is 0.001 (0.1%).
    /// </summary>
    public double ErrorRate { get; set; } = DefaultErrorRate;

    /// <summary>
    /// Gets or sets the delegate used to extract the deduplication key from a delivery context.
    /// </summary>
    public Func<WebhookDeliveryContext, string> KeySelector { get; set; } = DefaultKeySelector;

    /// <summary>
    /// Default key extraction logic combining EndpointId and Event type name + hash code.
    /// </summary>
    public static string DefaultKeySelector(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return $"{context.Endpoint.Id.Value}:{context.Event.GetType().Name}:{context.Event.GetHashCode()}";
    }

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    public void Validate() {
        Preca.ThrowIfLessThan(this.Capacity, 1);
        if(this.ErrorRate <= 0.0 || this.ErrorRate >= 1.0) {
            throw new ArgumentOutOfRangeException(nameof(this.ErrorRate), "Error rate must be between 0.0 and 1.0.");
        }
        Preca.ThrowIfNull(this.KeySelector);
    }
}
