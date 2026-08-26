using Wiaoj.Preconditions;

namespace Wiaoj.Webhooks.RateLimiting;

/// <summary>
/// Configuration options for outbound webhook rate limiting middleware.
/// </summary>
public sealed class WebhookRateLimitingOptions {
    /// <summary>
    /// Gets or sets the name of the rate limiting policy to apply.
    /// When <see langword="null"/>, the default rate limiting policy is used.
    /// </summary>
    public string? PolicyName { get; set; }

    /// <summary>
    /// Gets or sets the delegate used to extract the rate-limiting key from a delivery context.
    /// Defaults to <c>"wh:ratelimit:{EndpointId}"</c>.
    /// </summary>
    public Func<WebhookDeliveryContext, string> KeySelector { get; set; } = DefaultKeySelector;

    /// <summary>
    /// Gets or sets the delegate resolving the operation cost. Defaults to 1.
    /// </summary>
    public Func<WebhookDeliveryContext, int> CostResolver { get; set; } = static _ => 1;

    /// <summary>
    /// Default key extraction logic formatting as <c>wh:ratelimit:{EndpointId}</c>.
    /// </summary>
    public static string DefaultKeySelector(WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return $"wh:ratelimit:{context.Endpoint.Id.Value}";
    }
}