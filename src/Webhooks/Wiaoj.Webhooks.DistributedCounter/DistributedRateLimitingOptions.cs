using Wiaoj.Preconditions;

namespace Wiaoj.Webhooks.DistributedCounter;

/// <summary>
/// Configuration options for distributed per-endpoint webhook rate limiting.
/// </summary>
public sealed class DistributedRateLimitingOptions {
    /// <summary>
    /// The default maximum number of requests allowed per rate limiting window.
    /// </summary>
    public const long DefaultMaxRequestsPerWindow = 50;

    /// <summary>
    /// The default sliding time window duration (1 second).
    /// </summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum number of requests allowed within the <see cref="Window"/>. Default is 50.
    /// </summary>
    public long MaxRequestsPerWindow { get; set; } = DefaultMaxRequestsPerWindow;

    /// <summary>
    /// Gets or sets the sliding window duration. Default is 1 second.
    /// </summary>
    public TimeSpan Window { get; set; } = DefaultWindow;

    /// <summary>
    /// Gets or sets the delegate used to extract the counter key from a delivery context.
    /// </summary>
    public Func<WebhookDeliveryContext, string> KeySelector { get; set; } = DefaultKeySelector;

    /// <summary>
    /// Default key extraction logic formatting as <c>webhook:ratelimit:{EndpointId}</c>.
    /// </summary>
    public static string DefaultKeySelector(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return $"webhook:ratelimit:{context.Endpoint.Id.Value}";
    }

    /// <summary>
    /// Validates the configuration parameters.
    /// </summary>
    public void Validate() {
        Preca.ThrowIfLessThan(this.MaxRequestsPerWindow, 1);
        if(this.Window <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.Window), "Rate limit window must be a positive non-zero duration.");
        }
        Preca.ThrowIfNull(this.KeySelector);
    }
}
