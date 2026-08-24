namespace Wiaoj.Webhooks.Idempotency;

/// <summary>
/// Configuration options for outbound webhook idempotency enforcement.
/// </summary>
public sealed class IdempotencyOptions {
    /// <summary>
    /// The default deduplication time window (24 hours).
    /// </summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the time window during which duplicate events are suppressed. Default is 24 hours.
    /// </summary>
    public TimeSpan Window { get; set; } = DefaultWindow;

    /// <summary>
    /// Gets or sets a value indicating whether manual replay deliveries bypass the idempotency store check.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool BypassOnReplay { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional custom key selector delegate overriding the registered <see cref="IIdempotencyKeyGenerator"/>.
    /// </summary>
    public Func<WebhookDeliveryContext, IdempotencyKey>? CustomKeySelector { get; set; }

    /// <summary>
    /// Validates the configuration values.
    /// </summary>
    public void Validate() {
        if(this.Window <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(this.Window), "Idempotency window must be a positive non-zero duration.");
        }
    }
}