namespace Wiaoj.Webhooks.Retries;

/// <summary>
/// Implements a fixed-interval retry strategy where the delay between attempts remains constant.
/// </summary>
public sealed class FixedIntervalBackoffPolicy : IWebhookRetryPolicy {
    private readonly int _maxAttempts;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedIntervalBackoffPolicy"/> class with default settings (3 attempts, 5 second interval).
    /// </summary>
    public FixedIntervalBackoffPolicy() : this(3, TimeSpan.FromSeconds(5)) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedIntervalBackoffPolicy"/> class with specified parameters.
    /// </summary>
    /// <param name="maxAttempts">The maximum total number of delivery attempts.</param>
    /// <param name="interval">The constant delay interval between retry attempts.</param>
    public FixedIntervalBackoffPolicy(int maxAttempts, TimeSpan interval) {
        Preca.ThrowIfLessThan(maxAttempts, 1);
        if(interval < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval cannot be negative.");
        }

        this._maxAttempts = maxAttempts;
        this._interval = interval;
    }

    /// <inheritdoc/>
    public bool ShouldRetry(WebhookDeliveryContext context, WebhookDeliveryResult lastResult, out TimeSpan nextDelay) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(lastResult);

        nextDelay = TimeSpan.Zero;

        if(lastResult is not WebhookDeliveryResult.TransientFailure) {
            return false;
        }

        int completedAttempts = context.AttemptHistory.Count + 1;
        if(completedAttempts >= this._maxAttempts) {
            return false;
        }

        nextDelay = this._interval;
        return true;
    }
}