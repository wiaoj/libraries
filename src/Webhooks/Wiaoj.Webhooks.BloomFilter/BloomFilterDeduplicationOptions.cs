using Wiaoj.Preconditions;
using Wiaoj.Primitives.Hashing;

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
    /// Default key extraction logic combining EndpointId, Event type name, and 128-bit payload digest.
    /// </summary>
    public static string DefaultKeySelector(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        XxHash128 hash = XxHash128.Compute(context.SerializedPayload);
        return $"{context.Endpoint.Id.Value}:{context.Event.GetType().Name}:{hash}";
    }

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    public void Validate() {
        Preca.ThrowIfLessThan(this.Capacity, 1);
        if(this.ErrorRate is <= 0.0 or >= 1.0) {
            throw new ArgumentOutOfRangeException(nameof(this.ErrorRate), "Error rate must be between 0.0 and 1.0.");
        }
        Preca.ThrowIfNull(this.KeySelector);
    }
}